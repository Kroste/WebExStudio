using System.Collections.ObjectModel;
using Avalonia.Threading;
using ReactiveUI;
using WebExStudio.Core.Models;
using WebExStudio.Core.Serialization;
using WebExStudio.Core.Validation;
using WebExStudio.Engine;

namespace WebExStudio.UI.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private bool _isRunning;
    private bool _isPaused;
    private string _statusText = "Bereit";
    private string _projectDir = string.Empty;
    private CancellationTokenSource? _runCts;
    private Task? _runTask;
    private TaskCompletionSource? _pauseTcs;

    public Task? RunTask => _runTask;

    public FlowEditorViewModel FlowEditor { get; } = new();
    public TracePanelViewModel TracePanel { get; } = new();

    public RunConfig RunConfig { get; } = new();

    public MainWindowViewModel()
    {
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
        }
    }

    /// <summary>True while the flow is paused at a debug node, waiting for the user to resume.</summary>
    public bool IsPaused
    {
        get => _isPaused;
        private set => this.RaiseAndSetIfChanged(ref _isPaused, value);
    }

    public bool CanRun => !IsRunning && FlowEditor.Document is not null;
    public bool CanStop => IsRunning;

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
        StatusText = $"Flow geladen: {Path.GetFileName(path)}";
    }

    public async Task SaveFlowAsync(string path)
    {
        await FlowEditor.SaveAsync(path);
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
            // Run the executor on a background thread so the UI thread stays free to
            // render node highlights; trace updates marshal back via Progress<T>.
            await Task.Run(() =>
                executor.RunDocumentAsync(doc, RunConfig,
                    new TargetConfig { Name = "Lokal", Enabled = true },
                    progress, ct, OnPauseRequested), ct);
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

    /// <summary>Resumes a flow paused at a debug node.</summary>
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
