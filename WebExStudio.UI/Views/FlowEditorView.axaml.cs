using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using NLog;
using WebExStudio.UI.Controls;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Views;

public partial class FlowEditorView : UserControl
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private FlowEditorViewModel? Vm => DataContext as FlowEditorViewModel;
    private readonly Dictionary<string, NodeControl> _nodeControls = [];

    // Tracks which VMs already have a PropertyChanged handler so we never register twice.
    private readonly HashSet<string> _vmHandlersRegistered = [];

    public FlowEditorView()
    {
        InitializeComponent();
        Canvas.GridOverlay = GridOverlay;
        ConnectionRenderer.RenderTransform = Canvas.WorldTransform;
        DataContextChanged += OnDataContextChanged;
        PointerPressed += OnCanvasPointerPressed;
    }

    public void ResetView() => Canvas.ResetView();

    public void FitToView()
    {
        var bounds = _nodeControls.Values
            .Select(ctrl => new Rect(ctrl.ViewModel.X, ctrl.ViewModel.Y,
                                     ctrl.ViewModel.Width, ctrl.ViewModel.Height));
        Canvas.FitToView(bounds);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (Vm is null) return;
        Log.Debug("DataContext geändert, registriere Collection-Listener");
        _vmHandlersRegistered.Clear();
        Vm.Nodes.CollectionChanged += (_, _) => RebuildNodes();
        Vm.Connections.CollectionChanged += (_, _) => RefreshConnections();
        RebuildNodes();
        RefreshConnections();
    }

    private void RebuildNodes()
    {
        Canvas.Children.Clear();
        _nodeControls.Clear();

        if (Vm is null) return;

        Log.Debug("RebuildNodes: {0} Top-Level-Nodes", Vm.Nodes.Count);
        foreach (var nodeVm in Vm.Nodes)
            AddNodeTree(nodeVm);

        Vm.RebuildConnections();
    }

    private void AddNodeTree(NodeViewModel nodeVm)
    {
        AddNodeControl(nodeVm);
        if (nodeVm.IsExpanded && nodeVm.HasSubActions)
        {
            var subCount = nodeVm.AllSubNodes.Count();
            Log.Debug("AddNodeTree: {0} '{1}' expanded, {2} Sub-Nodes", nodeVm.Id, nodeVm.ActionType, subCount);
            foreach (var child in nodeVm.AllSubNodes)
                AddNodeTree(child);
        }
    }

    private void AddNodeControl(NodeViewModel nodeVm)
    {
        var ctrl = new NodeControl(nodeVm);
        ctrl.PointerPressed += OnNodePointerPressed;
        ctrl.DeleteRequested += OnNodeDeleteRequested;
        _nodeControls[nodeVm.Id] = ctrl;
        Canvas.Children.Add(ctrl);
        Avalonia.Controls.Canvas.SetLeft(ctrl, nodeVm.X);
        Avalonia.Controls.Canvas.SetTop(ctrl, nodeVm.Y);

        // Only register PropertyChanged handler once per VM lifetime.
        // The handler looks up the current ctrl from _nodeControls so it stays correct
        // even after RebuildNodes replaces the NodeControl instance.
        if (_vmHandlersRegistered.Add(nodeVm.Id))
        {
            nodeVm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(NodeViewModel.IsExpanded))
                {
                    Log.Debug("Node {0} ({1}) IsExpanded → {2}", nodeVm.Id, nodeVm.ActionType, nodeVm.IsExpanded);
                    RebuildNodes();
                }
                else if (args.PropertyName is nameof(NodeViewModel.X) or nameof(NodeViewModel.Y))
                {
                    if (_nodeControls.TryGetValue(nodeVm.Id, out var currentCtrl))
                    {
                        Avalonia.Controls.Canvas.SetLeft(currentCtrl, nodeVm.X);
                        Avalonia.Controls.Canvas.SetTop(currentCtrl, nodeVm.Y);
                    }
                    RefreshConnections();
                }
            };
        }
    }

    private void OnNodePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not NodeControl ctrl || Vm is null) return;
        Log.Debug("Node ausgewählt: {0} ({1})", ctrl.ViewModel.Id, ctrl.ViewModel.ActionType);
        Vm.SelectedNode = ctrl.ViewModel;

        if (e.GetCurrentPoint(Canvas).Properties.IsLeftButtonPressed)
        {
            Canvas.BeginNodeDrag(ctrl.ViewModel, e.GetPosition(Canvas), e);
            e.Handled = true;
        }
    }

    private void OnNodeDeleteRequested(object? sender, NodeViewModel vm)
    {
        Log.Info("Node löschen angefordert: {0} ({1})", vm.Id, vm.ActionType);
        _vmHandlersRegistered.Remove(vm.Id);
        Vm?.DeleteNode(vm);
        RebuildNodes();
    }

    private void RefreshConnections()
    {
        if (Vm is null) return;
        ConnectionRenderer.Update(Vm.Connections, Vm.Nodes);
        ConnectionRenderer.InvalidateVisual();
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed) return;

        // Only show the add-node menu when clicking on the canvas background, not on a node
        var elem = e.Source as Avalonia.StyledElement;
        while (elem is not null)
        {
            if (elem is NodeControl) return;
            elem = elem.Parent;
        }

        ShowContextMenu(e.GetPosition(Canvas));
        e.Handled = true;
    }

    private void ShowContextMenu(Point canvasPos)
    {
        var worldPos = Canvas.CanvasToWorld(canvasPos);
        Log.Debug("Kontext-Menü öffnen @ ({0:F0},{1:F0})", worldPos.X, worldPos.Y);
        var menu = new ContextMenu();

        foreach (var category in Core.Models.NodeCatalog.Categories)
        {
            var header = new MenuItem { Header = category };
            foreach (var def in Core.Models.NodeCatalog.GetByCategory(category))
            {
                var d = def;
                var item = new MenuItem { Header = $"{d.Icon}  {d.DisplayName}" };
                item.Click += (_, _) =>
                {
                    Log.Info("Node hinzufügen: {0} @ ({1:F0},{2:F0})", d.Type, worldPos.X, worldPos.Y);
                    Vm?.AddNode(d.Type, worldPos.X, worldPos.Y);
                };
                header.Items.Add(item);
            }
            menu.Items.Add(header);
        }

        menu.Open(this);
    }
}
