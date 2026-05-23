using System.Collections.ObjectModel;
using ReactiveUI;
using WebExStudio.Core.Models;
using WebExStudio.Core.Serialization;
using WebExStudio.Engine;

namespace WebExStudio.UI.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private bool _isRunning;
    private string _statusText = "Bereit";
    private string _projectDir = string.Empty;
    private CancellationTokenSource? _runCts;
    private Task? _runTask;

    public Task? RunTask => _runTask;

    public FlowEditorViewModel FlowEditor { get; } = new();
    public TracePanelViewModel TracePanel { get; } = new();
    public ObservableCollection<TargetViewModel> Targets { get; } = [];

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

    public bool CanRun => !IsRunning && (Targets.Any(t => t.Enabled) || FlowEditor.Document is not null);
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

        var targetsPath = Path.Combine(projectDir, "targets.json");
        if (File.Exists(targetsPath))
        {
            var configs = await FlowSerializer2.LoadTargetsAsync(targetsPath);
            Targets.Clear();
            foreach (var t in configs)
                Targets.Add(new TargetViewModel(t));
            this.RaisePropertyChanged(nameof(CanRun));
        }

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
            var enabled = Targets.Where(t => t.Enabled).Select(t => t.Model).ToList();
            if (enabled.Count > 0)
            {
                await executor.RunProjectAsync(RunConfig, enabled, progress, _runCts.Token);
            }
            else if (FlowEditor.Document is { } doc)
            {
                if (string.IsNullOrEmpty(RunConfig.ProjectDir))
                    RunConfig.ProjectDir = doc.FilePath is { } fp
                        ? Path.GetDirectoryName(fp) ?? Environment.CurrentDirectory
                        : Environment.CurrentDirectory;
                var target = new TargetConfig { Name = "Lokal", Enabled = true };
                await executor.RunDocumentAsync(doc, RunConfig, target, progress, _runCts.Token);
            }
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
            FlowEditor.ClearExecutionState();
            _runCts?.Dispose();
            _runCts = null;
        }
    }

    public void NotifyChanged(string propertyName) =>
        ((ReactiveUI.IReactiveObject)this).RaisePropertyChanged(propertyName);

    public void StartRun() => _runTask = RunAsync();

    public void StopRun()
    {
        _runCts?.Cancel();
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
            FlowEditor.SetActiveNode(entry.NodeId);
        else
            FlowEditor.SetNodeStatus(entry.NodeId, uiStatus);
    }
}
