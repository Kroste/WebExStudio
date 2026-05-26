using System.Collections.ObjectModel;
using Avalonia.Threading;
using ReactiveUI;
using WebExStudio.AI;
using WebExStudio.Core.Models;
using WebExStudio.Core.Serialization;
using WebExStudio.Core.Validation;
using WebExStudio.Engine;

namespace WebExStudio.UI.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private bool _isRunning;
    private bool _isPaused;
    private bool _suggestionsEnabled = true;
    private string _statusText = "Bereit";
    private string _projectDir = string.Empty;
    private CancellationTokenSource? _runCts;
    private Task? _runTask;
    private TaskCompletionSource? _pauseTcs;

    public Task? RunTask => _runTask;

    public FlowEditorViewModel FlowEditor { get; } = new();
    public TracePanelViewModel TracePanel { get; } = new();

    public RunConfig RunConfig { get; } = new();

    /// <summary>KI-Anbindung (Anbieter/Key/Modell) — aus den Einstellungen befüllt.</summary>
    public AiOptions AiOptions { get; } = new();

    /// <summary>Mehr-Turn-Chat mit der KI (Verlauf bleibt erhalten, auch wenn das Fenster schließt).</summary>
    public ChatViewModel Chat { get; }

    /// <summary>Zuletzt geöffnete/gespeicherte Flow-Dateien (persistiert in den Einstellungen).</summary>
    public ObservableCollection<string> RecentFiles { get; } = [];

    /// <summary>Setzt die Liste beim Start (ohne erneutes Speichern).</summary>
    public void InitRecentFiles(IEnumerable<string> paths)
    {
        RecentFiles.Clear();
        foreach (var p in paths) RecentFiles.Add(p);
    }

    private void AddRecent(string path)
    {
        RecentFiles.Remove(path);
        RecentFiles.Insert(0, path);
        while (RecentFiles.Count > 8) RecentFiles.RemoveAt(8);
        AppSettings.SaveRecentFiles([.. RecentFiles]);
    }

    /// <summary>Schaltet den KI-Node-Vorschlag (Toolbar 💡) ein/aus. Wird persistiert.</summary>
    public bool SuggestionsEnabled
    {
        get => _suggestionsEnabled;
        set
        {
            if (_suggestionsEnabled == value) return;
            this.RaiseAndSetIfChanged(ref _suggestionsEnabled, value);
            AppSettings.SaveSuggestionsEnabled(value);
        }
    }

    /// <summary>Setzt den Wert beim Start, ohne ihn erneut zu speichern.</summary>
    public void InitSuggestionsEnabled(bool enabled) =>
        this.RaiseAndSetIfChanged(ref _suggestionsEnabled, enabled, nameof(SuggestionsEnabled));

    /// <summary>Kurzstatus der KI-Anbindung für die Statusleiste.</summary>
    public string AiStatus => AiOptions.IsConfigured
        ? $"KI: {AiOptions.Provider}"
        : "KI: nicht konfiguriert";

    /// <summary>Nach Änderungen in den Einstellungen die KI-Anzeige aktualisieren.</summary>
    public void NotifyAiSettingsChanged() => this.RaisePropertyChanged(nameof(AiStatus));

    /// <summary>
    /// Erzeugt per KI einen Flow aus einer Beschreibung und lädt ihn bei Erfolg in den Editor.
    /// Liefert das Ergebnis zurück, damit der Aufrufer Fehler/Validierung anzeigen kann.
    /// Nutzt denselben Proxy wie der Browser (aus <see cref="RunConfig"/>).
    /// </summary>
    public async Task<FlowGenerationResult> GenerateFlowAsync(string description, CancellationToken ct = default)
    {
        using var http = ProxyFactory.CreateHttpClient(
            RunConfig.ProxyServer, RunConfig.ProxyBypass, RunConfig.ProxyUsername, RunConfig.ProxyPassword,
            TimeSpan.FromMinutes(2));
        var client = LlmClientFactory.Create(AiOptions, http);
        var generator = new FlowGenerator(client);
        var result = await generator.GenerateAsync(description, AiOptions.ActiveHints, ct);

        if (result.Success && result.Document is not null)
        {
            FlowEditor.LoadDocument(result.Document);
            FlowEditor.MarkDirty();
            StatusText = $"Flow von KI erzeugt ({result.Document.Nodes.Count} Nodes) — bitte prüfen und speichern";
        }
        return result;
    }

    /// <summary>Fragt die KI nach dem nächsten Node hinter dem Anker-Node.</summary>
    public async Task<NodeSuggestionResult> SuggestNextNodeAsync(NodeViewModel anchor, CancellationToken ct = default)
    {
        if (FlowEditor.Document is null)
            return NodeSuggestionResult.Failed("Kein Flow geöffnet.");

        using var http = ProxyFactory.CreateHttpClient(
            RunConfig.ProxyServer, RunConfig.ProxyBypass, RunConfig.ProxyUsername, RunConfig.ProxyPassword,
            TimeSpan.FromMinutes(2));
        var suggester = new NodeSuggester(LlmClientFactory.Create(AiOptions, http));
        var flowJson = FlowSerializer2.Serialize(FlowEditor.Document);
        return await suggester.SuggestAsync(flowJson, anchor.Id, anchor.ActionType, AiOptions.ActiveHints, ct);
    }

    /// <summary>Übernimmt einen Vorschlag: neuer Node hinter dem Anker, verbunden.</summary>
    public void ApplySuggestion(NodeViewModel anchor, NodeSuggestion suggestion)
    {
        FlowEditor.AddConnectedNode(anchor, suggestion.Type, suggestion.Config, suggestion.Label);
        StatusText = $"Node '{suggestion.Type}' hinzugefügt";
    }

    public MainWindowViewModel()
    {
        Chat = new ChatViewModel(this);
        FlowEditor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(FlowEditorViewModel.Document) or nameof(FlowEditorViewModel.CanSave))
                this.RaisePropertyChanged(nameof(CanRun));
        };
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isRunning, value);
            this.RaisePropertyChanged(nameof(CanRun));
            this.RaisePropertyChanged(nameof(CanStop));
            this.RaisePropertyChanged(nameof(CanPause));
            this.RaisePropertyChanged(nameof(CanStep));
        }
    }

    /// <summary>True while the flow is paused (manuell oder an einem Debug-Node).</summary>
    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isPaused, value);
            this.RaisePropertyChanged(nameof(CanPause));
            this.RaisePropertyChanged(nameof(CanStep));
        }
    }

    public bool CanRun => !IsRunning && FlowEditor.Document is not null;
    public bool CanStop => IsRunning;
    public bool CanPause => IsRunning && !IsPaused;
    public bool CanStep => IsRunning && IsPaused;

    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public string ProjectDir
    {
        get => _projectDir;
        set => this.RaiseAndSetIfChanged(ref _projectDir, value);
    }

    public void NewFlow()
    {
        FlowEditor.NewDocument();
        StatusText = "Neuer Flow";
    }

    public async Task OpenFlowAsync(string path)
    {
        await FlowEditor.LoadAsync(path);
        AddRecent(path);
        StatusText = $"Flow geladen: {Path.GetFileName(path)}";
    }

    public async Task SaveFlowAsync(string path)
    {
        await FlowEditor.SaveAsync(path);
        AddRecent(path);
        StatusText = "Gespeichert";
    }

    public async Task RunAsync()
    {
        if (IsRunning) return;

        var doc = FlowEditor.Document;
        if (doc is null) { StatusText = "Kein Flow geöffnet"; return; }

        TracePanel.Clear();
        FlowEditor.ClearExecutionState();

        // Vor dem Lauf validieren: Fehler brechen ab, Warnungen werden nur angezeigt.
        var validation = FlowValidator.Validate(doc);
        ShowValidationIssues(validation);
        if (!validation.IsValid)
        {
            foreach (var issue in validation.Errors.Where(i => i.NodeId is not null))
                FlowEditor.SetNodeStatus(issue.NodeId!, ExecutionStatusUi.Error);

            // Zum ersten fehlerhaften Node springen, damit die rote Markierung sichtbar ist.
            var firstNodeError = validation.Errors.FirstOrDefault(i => i.NodeId is not null);
            if (firstNodeError?.NodeId is { } fid && FlowEditor.FindTabOfNode(fid) is { } tab)
                FlowEditor.OpenTab(tab);

            var errorCount = validation.Errors.Count();
            StatusText = $"Ausführung abgebrochen: {errorCount} Validierungsfehler — siehe Protokoll";
            return;
        }

        IsRunning = true;
        StatusText = "Ausführung läuft…";

        _runCts = new CancellationTokenSource();
        var progress = new Progress<TraceEntry>(OnTraceEntry);
        var executor = new FlowExecutor();

        try
        {
            if (string.IsNullOrEmpty(RunConfig.ProjectDir))
                RunConfig.ProjectDir = doc.FilePath is { } fp
                    ? Path.GetDirectoryName(fp) ?? Environment.CurrentDirectory
                    : Environment.CurrentDirectory;

            var ct = _runCts.Token;
            // KI-Callback für den ai_query-Node: baut bei Bedarf den konfigurierten LLM-Client
            // (gleicher Proxy wie der Browser) und liefert die Antwort zurück. null = KI aus.
            Func<AiRequest, CancellationToken, Task<string>>? aiComplete = null;
            if (AiOptions.IsConfigured)
                aiComplete = async (req, token) =>
                {
                    // Optionale Anbieter-/Modell-Auswahl des Nodes anwenden (sonst Einstellungen).
                    var opts = AiOptions;
                    if (!string.IsNullOrWhiteSpace(req.Provider) || !string.IsNullOrWhiteSpace(req.Model))
                    {
                        var providerChanged = !string.IsNullOrWhiteSpace(req.Provider)
                            && !string.Equals(req.Provider, AiOptions.Provider, StringComparison.OrdinalIgnoreCase);
                        opts = new AiOptions
                        {
                            Provider = string.IsNullOrWhiteSpace(req.Provider) ? AiOptions.Provider : req.Provider!,
                            // Modell: explizit > bei anderem Anbieter dessen Standard ("") > Einstellungen
                            Model = !string.IsNullOrWhiteSpace(req.Model) ? req.Model! : providerChanged ? "" : AiOptions.Model,
                            ApiKey = AiOptions.ApiKey,
                            BaseUrl = providerChanged ? "" : AiOptions.BaseUrl,
                        };
                    }
                    using var http = ProxyFactory.CreateHttpClient(
                        RunConfig.ProxyServer, RunConfig.ProxyBypass, RunConfig.ProxyUsername, RunConfig.ProxyPassword,
                        TimeSpan.FromMinutes(2));
                    var client = LlmClientFactory.Create(opts, http);
                    return await client.ChatAsync(req.SystemPrompt, [new ChatMessage(ChatRole.User, req.UserPrompt)], req.JsonMode, token);
                };

            // Run the executor on a background thread so the UI thread stays free to
            // render node highlights; trace updates marshal back via Progress<T>.
            await Task.Run(() =>
                executor.RunDocumentAsync(doc, RunConfig,
                    new TargetConfig { Name = "Lokal", Enabled = true },
                    progress, ct, OnPauseRequested, PauseGateAsync, aiComplete), ct);
            StatusText = "Ausführung abgeschlossen";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Ausführung abgebrochen";
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
            IsPaused = false;
            _pauseTcs = null;
            FlowEditor.ClearExecutionState();
            _runCts?.Dispose();
            _runCts = null;
        }
    }

    /// <summary>Schreibt die Validierungsbefunde als Trace-Einträge ins Protokoll-Panel.</summary>
    private void ShowValidationIssues(FlowValidationResult result)
    {
        if (result.Issues.Count == 0) return;
        var now = DateTime.Now;
        var empty = new Dictionary<string, string>();

        foreach (var issue in result.Issues)
        {
            var isError = issue.Severity == FlowIssueSeverity.Error;
            TracePanel.AddEntry(new TraceEntry(
                NodeId: issue.NodeId ?? string.Empty,
                ActionType: "Validierung",
                Status: isError ? ExecutionStatus.Error : ExecutionStatus.Skipped,
                Timestamp: now,
                TargetName: issue.Code,
                ContextSnapshot: empty,
                Message: $"{(isError ? "✖" : "⚠")} {issue.Message}"));
        }
    }

    /// <summary>Invoked by the debug node (on a background thread) to pause until the user resumes.</summary>
    private Task OnPauseRequested(string message)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _pauseTcs = tcs;
        Dispatcher.UIThread.Post(() =>
        {
            StatusText = "Pausiert — auf „Weiter“ warten…";
            IsPaused = true;
        });
        return tcs.Task;
    }

    /// <summary>Manuelles Pausieren: hält die Ausführung vor dem nächsten Node an.</summary>
    public void Pause()
    {
        if (!IsRunning || IsPaused) return;
        _pauseTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IsPaused = true;
        StatusText = "Pausiert — auf „Weiter“ warten…";
    }

    /// <summary>Gate für den Executor: wartet, solange pausiert ist (vor jedem Node geprüft).
    /// Markiert dabei den anstehenden Node als „nächsten".</summary>
    private Task PauseGateAsync(FlowNode node)
    {
        var tcs = _pauseTcs;
        if (tcs is null) return Task.CompletedTask; // nicht pausiert → frei laufen
        Dispatcher.UIThread.Post(() => FlowEditor.SetNextNode(node.Id));
        return tcs.Task;
    }

    /// <summary>
    /// Einzelschritt: lässt genau einen Node laufen und pausiert danach wieder. Nur sinnvoll,
    /// wenn bereits pausiert. Setzt zuerst ein neues Gate für den nächsten Node, gibt dann den
    /// aktuellen Node frei.
    /// </summary>
    public void Step()
    {
        if (!IsRunning || !IsPaused) return;
        var current = _pauseTcs;
        _pauseTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        StatusText = "Einzelschritt…";
        current?.TrySetResult(); // genau den nächsten Node freigeben; danach greift das neue Gate
    }

    /// <summary>Resumes a flow paused at a debug node or manually.</summary>
    public void Resume()
    {
        IsPaused = false;
        StatusText = "Ausführung läuft…";
        _pauseTcs?.TrySetResult();
        _pauseTcs = null;
    }

    public void StartRun() => _runTask = RunAsync();

    public void StopRun()
    {
        _runCts?.Cancel();
        // Release any pause so the cancellation can propagate.
        _pauseTcs?.TrySetResult();
        _pauseTcs = null;
        IsPaused = false;
        StatusText = "Wird abgebrochen…";
    }

    private void OnTraceEntry(TraceEntry entry)
    {
        TracePanel.AddEntry(entry);

        var uiStatus = entry.Status switch
        {
            ExecutionStatus.Running => ExecutionStatusUi.Running,
            ExecutionStatus.Success => ExecutionStatusUi.Success,
            ExecutionStatus.Error => ExecutionStatusUi.Error,
            ExecutionStatus.Skipped => ExecutionStatusUi.Skipped,
            _ => ExecutionStatusUi.None,
        };

        if (entry.Status == ExecutionStatus.Running)
        {
            FlowEditor.SetActiveNode(entry.NodeId);
            FlowEditor.SetNodeStatus(entry.NodeId, ExecutionStatusUi.Running);
        }
        else
        {
            FlowEditor.SetNodeStatus(entry.NodeId, uiStatus);
        }
    }
}
