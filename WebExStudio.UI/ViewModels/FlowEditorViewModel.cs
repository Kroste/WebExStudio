using System.Collections.ObjectModel;
using ReactiveUI;
using WebExStudio.Core.Models;
using WebExStudio.Core.Serialization;

namespace WebExStudio.UI.ViewModels;

public sealed class FlowEditorViewModel : ViewModelBase
{
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
        var doc = await FlowSerializer.LoadAsync(path);
        LoadDocument(doc);
    }

    public void LoadDocument(FlowDocument doc)
    {
        Document = doc;
        Nodes.Clear();
        Connections.Clear();

        foreach (var action in doc.Actions)
            Nodes.Add(new NodeViewModel(action));

        RebuildConnections();
        IsDirty = false;
        this.RaisePropertyChanged(nameof(Title));
    }

    public async Task SaveAsync(string path)
    {
        if (Document is null) return;
        SyncPositionsToModel();
        await FlowSerializer.SaveAsync(Document, path);
        IsDirty = false;
        this.RaisePropertyChanged(nameof(Title));
    }

    public void AddNode(string actionType, double x, double y)
    {
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
        Document?.Actions.Remove(node.Model);
        Nodes.Remove(node);
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
        foreach (var n in Nodes)
            n.IsActive = n.Id == nodeId;
    }

    public void SetNodeStatus(string nodeId, ExecutionStatusUi status)
    {
        var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is not null) node.Status = status;
    }

    public void ClearExecutionState()
    {
        foreach (var n in Nodes)
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

    /// <summary>
    /// Rebuilds the connection list from the current node order.
    /// Sequential connections: node[i].output → node[i+1].input
    /// </summary>
    public void RebuildConnections()
    {
        Connections.Clear();
        for (int i = 0; i < Nodes.Count - 1; i++)
            Connections.Add(new ConnectionViewModel(Nodes[i], Nodes[i + 1], ConnectionKind.Sequential));
    }

    private void SyncPositionsToModel()
    {
        foreach (var vm in Nodes)
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
