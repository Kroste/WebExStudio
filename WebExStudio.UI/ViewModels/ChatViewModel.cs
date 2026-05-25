using System.Collections.ObjectModel;
using NLog;
using ReactiveUI;
using WebExStudio.AI;
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
            Messages.Add(ChatTurnViewModel.Notice(
                "Keine KI-Anbindung konfiguriert. Bitte in den Einstellungen (Tab „KI“) einen "
                + "API-Schlüssel hinterlegen oder Ollama als Anbieter wählen."));
            return;
        }

        Input = string.Empty;
        Messages.Add(new ChatTurnViewModel(ChatRole.User, text));
        _history.Add(new ChatMessage(ChatRole.User, text));

        // Aktuellen Flow mitgeben, damit die KI auf dem echten Stand arbeitet (auch nach Edits).
        var flowJson = _main.FlowEditor.Document is { } doc ? FlowSerializer2.Serialize(doc) : null;
        await RunAssistantTurnAsync(PromptBuilder.BuildChatSystemPrompt(flowJson));
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
            Messages.Add(ChatTurnViewModel.Notice("Kein Flow geöffnet."));
            return;
        }
        if (!_main.AiOptions.IsConfigured)
        {
            Messages.Add(ChatTurnViewModel.Notice(
                "Keine KI-Anbindung konfiguriert. Bitte in den Einstellungen (Tab „KI“) einen "
                + "API-Schlüssel hinterlegen oder Ollama als Anbieter wählen."));
            return;
        }

        Messages.Add(new ChatTurnViewModel(ChatRole.User, "Bitte erkläre den aktuellen Flow."));
        // Dem Modell den vollständigen Flow mitgeben (nicht sichtbar im Chat).
        var json = FlowSerializer2.Serialize(doc);
        _history.Add(new ChatMessage(ChatRole.User,
            $"Erkläre den folgenden Flow:\n\n{json}"));

        await RunAssistantTurnAsync(PromptBuilder.BuildExplainSystemPrompt());
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

    /// <summary>Lädt einen in einer Antwort erkannten Flow in den Editor.</summary>
    public void LoadFlow(ChatTurnViewModel turn)
    {
        if (turn.Flow is null) return;
        _main.FlowEditor.LoadDocument(turn.Flow);
        _main.FlowEditor.MarkDirty();
        Messages.Add(ChatTurnViewModel.Notice("Flow in den Editor geladen — bitte prüfen und speichern."));
    }
}
