using System.Collections.ObjectModel;
using System.Text.Json;
using NLog;
using ReactiveUI;
using WebExStudio.Core.Models;
using WebExStudio.Core.Serialization;

namespace WebExStudio.UI.ViewModels;

public sealed class FlowEditorViewModel : ViewModelBase
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private FlowDocument? _document;
    private NodeViewModel? _selectedNode;
    private bool _isDirty;

    public ObservableCollection<NodeViewModel> Nodes { get; } = [];
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];

    public FlowDocument? Document
    {
        get => _document;
        private set => this.RaiseAndSetIfChanged(ref _document, value);
    }

    public NodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (_selectedNode != null) _selectedNode.IsSelected = false;
            this.RaiseAndSetIfChanged(ref _selectedNode, value);
            if (value != null) value.IsSelected = true;
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        set => this.RaiseAndSetIfChanged(ref _isDirty, value);
    }

    public string Title => Document is null
        ? "Kein Flow geöffnet"
        : $"{Document.DisplayName}{(IsDirty ? " *" : "")}";

    public async Task LoadAsync(string path)
    {
        Log.Info("Öffne Flow-Datei: {0}", path);
        var doc = await FlowSerializer.LoadAsync(path);
        await LoadDocumentAsync(doc);
    }

    public async Task LoadDocumentAsync(FlowDocument doc)
    {
        Log.Info("Lade Dokument: {0}", doc.DisplayName ?? "(unbenannt)");
        Document = doc;
        Nodes.Clear();
        Connections.Clear();

        var projectDir = doc.FilePath is not null ? InferProjectDir(doc.FilePath) : null;

        foreach (var action in doc.Actions)
            Nodes.Add(await CreateNodeVmAsync(action, projectDir));

        RebuildConnections();
        IsDirty = false;
        Log.Debug("Dokument geladen: {0} Nodes, {1} Verbindungen", Nodes.Count, Connections.Count);
        this.RaisePropertyChanged(nameof(Title));
    }

    public async Task SaveAsync(string path)
    {
        if (Document is null) return;
        Log.Info("Speichere Dokument: {0}", path);
        SyncPositionsToModel();
        SyncSubActionsToModel();
        await FlowSerializer.SaveAsync(Document, path);
        IsDirty = false;
        this.RaisePropertyChanged(nameof(Title));
    }

    public void AddNode(string actionType, double x, double y)
    {
        Log.Debug("Füge Node hinzu: {0} @ ({1:F0},{2:F0})", actionType, x, y);
        var node = new ActionNode { Type = actionType };
        node.EnsureUi(x, y);
        Document ??= new FlowDocument();
        Document.Actions.Add(node);
        var vm = new NodeViewModel(node);
        Nodes.Add(vm);
        RebuildConnections();
        MarkDirty();
    }

    public void DeleteNode(NodeViewModel node)
    {
        Log.Debug("Lösche Node: {0} ({1})", node.Id, node.ActionType);
        if (Document?.Actions.Remove(node.Model) == true)
            Nodes.Remove(node);
        else
            RemoveFromSubNodes(Nodes, node);

        if (SelectedNode == node) SelectedNode = null;
        RebuildConnections();
        MarkDirty();
    }

    public void MoveNode(NodeViewModel node, double dx, double dy)
    {
        node.X += dx;
        node.Y += dy;
        RebuildConnections();
        MarkDirty();
    }

    public void SetActiveNode(string nodeId)
    {
        foreach (var n in AllNodesFlat())
            n.IsActive = n.Id == nodeId;
    }

    public void SetNodeStatus(string nodeId, ExecutionStatusUi status)
    {
        var node = AllNodesFlat().FirstOrDefault(n => n.Id == nodeId);
        if (node is not null) node.Status = status;
    }

    public void ClearExecutionState()
    {
        foreach (var n in AllNodesFlat())
        {
            n.IsActive = false;
            n.Status = ExecutionStatusUi.None;
        }
    }

    public void MarkDirty()
    {
        IsDirty = true;
        this.RaisePropertyChanged(nameof(Title));
    }

    public void RebuildConnections()
    {
        Connections.Clear();
        BuildConnectionsForList(Nodes);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<NodeViewModel> CreateNodeVmAsync(ActionNode action, string? projectDir, bool expandCalls = true)
    {
        var vm = new NodeViewModel(action);
        if (!vm.HasSubActions) return vm;

        if (action.Type == "if_then_else")
        {
            var thenActions = action.GetSubActions("then");
            var fromFile = false;

            if (thenActions.Count == 0)
            {
                var thenFile = action.GetString("then_actions_file");
                if (!string.IsNullOrEmpty(thenFile) && projectDir is not null)
                {
                    var full = Path.IsPathRooted(thenFile)
                        ? thenFile
                        : Path.Combine(projectDir, thenFile);
                    if (File.Exists(full))
                    {
                        Log.Debug("if_then_else lädt then-Datei: {0}", full);
                        var sub = await FlowSerializer.LoadAsync(full, applyLayout: false);
                        thenActions = sub.Actions;
                        fromFile = true;
                    }
                    else
                    {
                        Log.Warn("then_actions_file nicht gefunden: {0}", full);
                    }
                }
            }

            EnsureSubActionLayout(thenActions, action, -1, forceReset: fromFile);
            foreach (var a in thenActions)
                vm.ThenNodes.Add(await CreateNodeVmAsync(a, projectDir));
            vm.ThenFromFile = fromFile;

            var elseActions = action.GetSubActions("else");
            EnsureSubActionLayout(elseActions, action, +1);
            foreach (var a in elseActions)
                vm.ElseNodes.Add(await CreateNodeVmAsync(a, projectDir));
        }
        else if (action.Type == "call" && expandCalls)
        {
            var fileKey = action.GetString("file");
            if (string.IsNullOrEmpty(fileKey))
                fileKey = action.GetString("actions_file");

            Log.Debug("call-Node id={0} fileKey='{1}' projectDir='{2}'",
                action.EnsureUi().Id, fileKey, projectDir ?? "(null)");

            if (!string.IsNullOrEmpty(fileKey) && projectDir is not null)
            {
                var full = Path.IsPathRooted(fileKey)
                    ? fileKey
                    : Path.Combine(projectDir, fileKey);
                try
                {
                    if (File.Exists(full))
                    {
                        Log.Debug("call-Node lädt Sub-Flow: {0}", full);
                        var sub = await FlowSerializer.LoadAsync(full, applyLayout: false);
                        EnsureSubActionLayout(sub.Actions, action, 0, forceReset: true);
                        foreach (var a in sub.Actions)
                            vm.BodyNodes.Add(await CreateNodeVmAsync(a, projectDir, expandCalls: false));
                        Log.Info("call-Node {0}: Sub-Flow geladen, {1} Sub-Nodes ({2})",
                            action.EnsureUi().Id, vm.BodyNodes.Count, Path.GetFileName(full));
                    }
                    else
                    {
                        Log.Warn("call-Datei nicht gefunden: {0}", full);
                        vm.BodyNodes.Add(MakeErrorNode(action, $"Nicht gefunden: {full}"));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Fehler beim Laden von call-Sub-Flow: {0}", full);
                    vm.BodyNodes.Add(MakeErrorNode(action, ex.Message));
                }
            }
            else if (string.IsNullOrEmpty(fileKey))
            {
                Log.Warn("call-Node hat keine Dateiangabe (id={0})", action.EnsureUi().Id);
            }
            else
            {
                Log.Warn("call-Node {0}: projectDir ist null, Sub-Flow kann nicht geladen werden (fileKey='{1}')",
                    action.EnsureUi().Id, fileKey);
                vm.BodyNodes.Add(MakeErrorNode(action, $"Kein Projektverzeichnis bekannt (fileKey={fileKey})"));
            }
            vm.IsExpanded = false;
        }
        else if (action.Type != "call")
        {
            var bodyActions = action.GetSubActions("actions");
            EnsureSubActionLayout(bodyActions, action, 0);
            foreach (var a in bodyActions)
                vm.BodyNodes.Add(await CreateNodeVmAsync(a, projectDir));
        }

        return vm;
    }

    private static NodeViewModel MakeErrorNode(ActionNode parent, string message)
    {
        var err = new ActionNode { Type = "noop" };
        err.EnsureUi((parent.Ui?.X ?? 0) + 280, parent.Ui?.Y ?? 0);
        err.Properties = new Dictionary<string, JsonElement>
        {
            ["_error"] = JsonSerializer.SerializeToElement(message)
        };
        return new NodeViewModel(err);
    }

    private static string? InferProjectDir(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        // Flows live in {projectRoot}/actions/*.json — step up one level
        if (dir is not null && Path.GetFileName(dir).Equals("actions", StringComparison.OrdinalIgnoreCase))
            return Path.GetDirectoryName(dir);
        return dir;
    }

    private static void EnsureSubActionLayout(
        List<ActionNode> nodes, ActionNode parent, int side, bool forceReset = false)
    {
        var px = parent.Ui?.X ?? 0;
        var py = parent.Ui?.Y ?? 0;
        var ph = parent.Ui?.Height ?? 60;
        const double nodeWidth = 200;
        const double hGap = 80;
        const double vGap = 40;
        const double vStep = 120;

        double startX = side switch
        {
            +1 => px + 2 * (nodeWidth + hGap),
            _  => px + nodeWidth + hGap,
        };

        for (int i = 0; i < nodes.Count; i++)
        {
            if (forceReset || nodes[i].Ui is null)
            {
                var ui = nodes[i].EnsureUi();
                ui.X = startX;
                ui.Y = py + vGap + i * vStep;
            }
        }
    }

    private void BuildConnectionsForList(IList<NodeViewModel> nodes)
    {
        for (int i = 0; i < nodes.Count - 1; i++)
            Connections.Add(new ConnectionViewModel(nodes[i], nodes[i + 1], ConnectionKind.Sequential));

        foreach (var node in nodes)
        {
            if (!node.IsExpanded || !node.HasSubActions) continue;

            if (node.ThenNodes.Count > 0)
            {
                Connections.Add(new ConnectionViewModel(node, node.ThenNodes[0], ConnectionKind.TrueBranch));
                BuildConnectionsForList(node.ThenNodes);
            }
            if (node.ElseNodes.Count > 0)
            {
                Connections.Add(new ConnectionViewModel(node, node.ElseNodes[0], ConnectionKind.FalseBranch));
                BuildConnectionsForList(node.ElseNodes);
            }
            if (node.BodyNodes.Count > 0)
            {
                Connections.Add(new ConnectionViewModel(node, node.BodyNodes[0], ConnectionKind.LoopBody));
                BuildConnectionsForList(node.BodyNodes);
            }
        }
    }

    private void SyncSubActionsToModel()
    {
        foreach (var vm in Nodes)
            SyncNodeToModel(vm);
    }

    private static void SyncNodeToModel(NodeViewModel vm)
    {
        if (!vm.HasSubActions) return;

        foreach (var child in vm.AllSubNodes)
            SyncNodeToModel(child);

        vm.Model.Properties ??= [];

        if (vm.ActionType == "if_then_else")
        {
            // If ThenNodes came from a file, keep the file reference instead of inlining
            if (vm.ThenNodes.Count > 0 && !vm.ThenFromFile)
            {
                vm.Model.Properties["then"] = JsonSerializer.SerializeToElement(
                    vm.ThenNodes.Select(n => n.Model).ToList(),
                    FlowSerializerOptions.Default);
                vm.Model.Properties.Remove("then_actions_file");
            }
            if (vm.ElseNodes.Count > 0)
            {
                vm.Model.Properties["else"] = JsonSerializer.SerializeToElement(
                    vm.ElseNodes.Select(n => n.Model).ToList(),
                    FlowSerializerOptions.Default);
            }
        }
        else
        {
            if (vm.BodyNodes.Count > 0)
            {
                vm.Model.Properties["actions"] = JsonSerializer.SerializeToElement(
                    vm.BodyNodes.Select(n => n.Model).ToList(),
                    FlowSerializerOptions.Default);
            }
        }
    }

    private IEnumerable<NodeViewModel> AllNodesFlat() =>
        Nodes.SelectMany(FlattenNode);

    private static IEnumerable<NodeViewModel> FlattenNode(NodeViewModel vm)
    {
        yield return vm;
        foreach (var sub in vm.AllSubNodes)
            foreach (var desc in FlattenNode(sub))
                yield return desc;
    }

    private static bool RemoveFromSubNodes(IEnumerable<NodeViewModel> nodes, NodeViewModel target)
    {
        foreach (var n in nodes)
        {
            if (n.ThenNodes.Remove(target) || n.ElseNodes.Remove(target) || n.BodyNodes.Remove(target))
                return true;
            if (RemoveFromSubNodes(n.AllSubNodes.ToList(), target))
                return true;
        }
        return false;
    }

    private void SyncPositionsToModel()
    {
        foreach (var vm in AllNodesFlat())
        {
            var ui = vm.Model.EnsureUi();
            ui.X = vm.X;
            ui.Y = vm.Y;
        }
    }
}

public enum ConnectionKind { Sequential, TrueBranch, FalseBranch, LoopBody }

public sealed class ConnectionViewModel : ViewModelBase
{
    public NodeViewModel Source { get; }
    public NodeViewModel Target { get; }
    public ConnectionKind Kind { get; }

    public ConnectionViewModel(NodeViewModel source, NodeViewModel target, ConnectionKind kind)
    {
        Source = source;
        Target = target;
        Kind = kind;
    }

    public string StrokeColor => Kind switch
    {
        ConnectionKind.TrueBranch => "#4CAF50",
        ConnectionKind.FalseBranch => "#F44336",
        ConnectionKind.LoopBody => "#FF9800",
        _ => "#90A4AE",
    };
}
