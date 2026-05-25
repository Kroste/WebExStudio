using System.Collections.ObjectModel;
using NLog;
using ReactiveUI;
using WebExStudio.AI;

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

            var answer = await client.ChatAsync(PromptBuilder.BuildChatSystemPrompt(), _history);
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
