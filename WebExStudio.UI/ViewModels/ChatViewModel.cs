using System.Collections.ObjectModel;
using NLog;
using ReactiveUI;
using WebExStudio.AI;
using WebExStudio.Core.Localization;
using WebExStudio.Core.Logging;
using WebExStudio.Core.Serialization;

namespace WebExStudio.UI.ViewModels;

/// <summary>
/// Mehr-Turn-Chat mit der konfigurierten KI. Hält den Verlauf, schickt ihn bei jeder Frage
/// komplett mit und erkennt in Antworten enthaltene Flows (zum Laden in den Editor).
/// Nutzt dieselben Anbieter-/Proxy-Einstellungen wie der Rest der App.
/// </summary>
public sealed class ChatViewModel : ViewModelBase
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly MainWindowViewModel _main;
    private readonly List<ChatMessage> _history = [];

    public ObservableCollection<ChatTurnViewModel> Messages { get; } = [];

    public ChatViewModel(MainWindowViewModel main) => _main = main;

    /// <summary>Maskiert Geheimnisse (API-Key, Proxy-Passwort, sensible JSON-Felder) fürs Log.</summary>
    private string MaskSecrets(string text) =>
        SecretMasker.Mask(text, _main.AiOptions.ApiKey, _main.RunConfig.ProxyPassword);

    private string _input = string.Empty;
    public string Input
    {
        get => _input;
        set => this.RaiseAndSetIfChanged(ref _input, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isBusy, value);
            this.RaisePropertyChanged(nameof(CanSend));
        }
    }

    public bool CanSend => !IsBusy;

    public async Task SendAsync()
    {
        var text = Input?.Trim();
        if (string.IsNullOrEmpty(text) || IsBusy) return;

        if (!_main.AiOptions.IsConfigured)
        {
            Messages.Add(ChatTurnViewModel.Notice(Loc.T("Ns_NotConfigured")));
            return;
        }

        Input = string.Empty;
        Messages.Add(new ChatTurnViewModel(ChatRole.User, text));
        _history.Add(new ChatMessage(ChatRole.User, text));
        Log.Info("KI-Chat Anfrage ({0}): {1}", _main.AiOptions.Provider, MaskSecrets(text));

        // Aktuellen Flow mitgeben, damit die KI auf dem echten Stand arbeitet (auch nach Edits).
        var flowJson = _main.FlowEditor.Document is { } doc ? FlowSerializer2.Serialize(doc) : null;
        await RunAssistantTurnAsync(PromptBuilder.BuildChatSystemPrompt(flowJson, _main.AiOptions.ActiveHints));
    }

    /// <summary>
    /// Erklärt den aktuellen Flow: zeigt eine kurze Nutzer-Nachricht an, schickt aber den
    /// serialisierten Flow an das Modell und hängt die Erklärung als Antwort an.
    /// </summary>
    public async Task ExplainCurrentFlowAsync()
    {
        if (IsBusy) return;

        var doc = _main.FlowEditor.Document;
        if (doc is null)
        {
            Messages.Add(ChatTurnViewModel.Notice(Loc.T("VM_NoFlow")));
            return;
        }
        if (!_main.AiOptions.IsConfigured)
        {
            Messages.Add(ChatTurnViewModel.Notice(Loc.T("Ns_NotConfigured")));
            return;
        }

        Messages.Add(new ChatTurnViewModel(ChatRole.User, Loc.T("Chat_ExplainMsg")));
        Log.Info("KI-Chat: Flow erklären angefordert ({0} Nodes)", doc.Nodes.Count);
        // Dem Modell den vollständigen Flow mitgeben (nicht sichtbar im Chat).
        var json = FlowSerializer2.Serialize(doc);
        _history.Add(new ChatMessage(ChatRole.User,
            $"Erkläre den folgenden Flow:\n\n{json}"));

        await RunAssistantTurnAsync(PromptBuilder.BuildExplainSystemPrompt(_main.AiOptions.ActiveHints));
    }

    /// <summary>Ruft das Modell mit dem aktuellen Verlauf auf und hängt die Antwort als Turn an.</summary>
    private async Task RunAssistantTurnAsync(string systemPrompt)
    {
        var reply = new ChatTurnViewModel(ChatRole.Assistant, "…");
        Messages.Add(reply);
        IsBusy = true;

        try
        {
            var rc = _main.RunConfig;
            using var http = ProxyFactory.CreateHttpClient(
                rc.ProxyServer, rc.ProxyBypass, rc.ProxyUsername, rc.ProxyPassword,
                TimeSpan.FromMinutes(2));
            var client = LlmClientFactory.Create(_main.AiOptions, http);

            var answer = await client.ChatAsync(systemPrompt, _history);
            _history.Add(new ChatMessage(ChatRole.Assistant, answer));
            reply.Content = answer;
            reply.DetectFlow();
            if (reply.HasFlow) LatestFlowTurn = reply;
            Log.Info("KI-Chat Antwort ({0}, {1} Zeichen, Flow erkannt={2})",
                client.Name, answer.Length, reply.HasFlow);
            Log.Debug("KI-Chat Antwort-Inhalt: {0}", MaskSecrets(answer));
        }
        catch (Exception ex)
        {
            Log.Warn("KI-Chat fehlgeschlagen: {0}", ex.Message);
            reply.Content = $"⚠ Fehler: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Jüngste Antwort, die einen ladbaren Flow enthält (für den festen Lade-Balken).</summary>
    private ChatTurnViewModel? _latestFlowTurn;
    public ChatTurnViewModel? LatestFlowTurn
    {
        get => _latestFlowTurn;
        private set
        {
            this.RaiseAndSetIfChanged(ref _latestFlowTurn, value);
            this.RaisePropertyChanged(nameof(HasLoadableFlow));
            this.RaisePropertyChanged(nameof(LoadableFlowNote));
        }
    }

    public bool HasLoadableFlow => _latestFlowTurn is not null;
    public string LoadableFlowNote => _latestFlowTurn?.FlowNote ?? string.Empty;

    /// <summary>Lädt den zuletzt vorgeschlagenen Flow (vom festen Balken aus).</summary>
    public void LoadLatestFlow()
    {
        if (_latestFlowTurn is not null) LoadFlow(_latestFlowTurn);
    }

    /// <summary>Übernimmt eine (gekürzte) KI-Antwort als neuen Hinweis und speichert sie.</summary>
    public void RememberAsHint(ChatTurnViewModel turn)
    {
        var hint = ShortenForHint(turn.Content);
        if (string.IsNullOrWhiteSpace(hint)) return;

        var ai = _main.AiOptions;
        ai.Hints = string.IsNullOrWhiteSpace(ai.Hints) ? hint : ai.Hints.TrimEnd() + "\n" + hint;
        AppSettings.SaveAi(ai);
        _main.NotifyAiSettingsChanged();
        Log.Info("KI-Hinweis gemerkt: {0}", MaskSecrets(hint));
        Messages.Add(ChatTurnViewModel.Notice(Loc.T("Chat_HintSaved")));
    }

    private static string ShortenForHint(string text)
    {
        var collapsed = System.Text.RegularExpressions.Regex.Replace(text.Trim(), @"\s+", " ");
        const int max = 240;
        if (collapsed.Length > max) collapsed = collapsed[..max].TrimEnd() + "…";
        return collapsed.StartsWith('-') ? collapsed : "- " + collapsed;
    }

    /// <summary>Lädt einen in einer Antwort erkannten Flow in den Editor.</summary>
    public void LoadFlow(ChatTurnViewModel turn)
    {
        if (turn.Flow is null) return;
        Log.Info("Flow aus KI-Chat in Editor geladen ({0} Nodes)", turn.Flow.Nodes.Count);
        _main.FlowEditor.LoadDocument(turn.Flow);
        _main.FlowEditor.MarkDirty();
        Messages.Add(ChatTurnViewModel.Notice(Loc.T("Chat_FlowLoaded")));
    }
}
