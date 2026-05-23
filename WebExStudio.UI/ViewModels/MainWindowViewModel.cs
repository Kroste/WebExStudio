using System.Collections.ObjectModel;
using Avalonia.Threading;
using ReactiveUI;
using WebExStudio.Core.Models;
using WebExStudio.Core.Serialization;
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

    public async Task OpenProjectAsync(string projectDir)
    {
        ProjectDir = projectDir;
        RunConfig.ProjectDir = projectDir;

        var defaultFlow = Path.Combine(projectDir, "actions", "start.json");
        if (File.Exists(defaultFlow))
            await FlowEditor.LoadAsync(defaultFlow);

        StatusText = $"Projekt geladen: {projectDir}";
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

        IsRunning = true;
        TracePanel.Clear();
        FlowEditor.ClearExecutionState();
        StatusText = "Ausführung läuft…";

        _runCts = new CancellationTokenSource();
        var progress = new Progress<TraceEntry>(OnTraceEntry);
        var executor = new FlowExecutor();

        try
        {
            var doc = FlowEditor.Document;
            if (doc is null) { StatusText = "Kein Flow geöffnet"; return; }

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
