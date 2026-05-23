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
    private WireViewModel? _selectedWire;

    public FlowEditorView()
    {
        InitializeComponent();
        Focusable = true;
        Canvas.GridOverlay = GridOverlay;
        Canvas.ConnectionRenderer = ConnectionRenderer;
        ConnectionRenderer.RenderTransform = Canvas.WorldTransform;
        Canvas.WireDropped += OnWireDropped;
        DataContextChanged += OnDataContextChanged;
        PointerPressed += OnCanvasPointerPressed;

        // Palette drag-and-drop target
        DragDrop.SetAllowDrop(Canvas, true);
        Canvas.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        Canvas.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void SelectWire(WireViewModel? wire)
    {
        _selectedWire = wire;
        ConnectionRenderer.SelectedWire = wire;
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
        Log.Debug("DataContext geändert");
        Vm.PropertyChanged += OnVmPropertyChanged;
        RebuildCanvas();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FlowEditorViewModel.ActiveTab) or nameof(FlowEditorViewModel.Nodes))
            RebuildCanvas();
        else if (e.PropertyName == nameof(FlowEditorViewModel.Wires))
            RefreshConnections();
    }

    // The Nodes collection we're currently observing for add/remove (palette + right-click).
    private System.Collections.Specialized.INotifyCollectionChanged? _observedNodes;

    private void RebuildCanvas()
    {
        // Stop observing the previous tab's collection
        if (_observedNodes is not null)
            _observedNodes.CollectionChanged -= OnNodesCollectionChanged;
        _observedNodes = null;

        SelectWire(null);
        Canvas.Children.Clear();
        _nodeControls.Clear();

        if (Vm?.ActiveTab is null) return;

        Log.Debug("RebuildCanvas: Tab={0}, {1} Nodes", Vm.ActiveTab.Label, Vm.ActiveTab.Nodes.Count);

        foreach (var nodeVm in Vm.ActiveTab.Nodes)
            AddNodeControl(nodeVm);

        RefreshConnections();
        UpdateTabButtonStyles();

        // Observe the active tab's node collection so additions from any source render
        _observedNodes = Vm.ActiveTab.Nodes;
        _observedNodes.CollectionChanged += OnNodesCollectionChanged;
    }

    private void OnNodesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case System.Collections.Specialized.NotifyCollectionChangedAction.Add when e.NewItems is not null:
                foreach (NodeViewModel vm in e.NewItems)
                    if (!_nodeControls.ContainsKey(vm.Id))
                        AddNodeControl(vm);
                break;
            case System.Collections.Specialized.NotifyCollectionChangedAction.Remove when e.OldItems is not null:
                foreach (NodeViewModel vm in e.OldItems)
                    if (_nodeControls.Remove(vm.Id, out var ctrl))
                        Canvas.Children.Remove(ctrl);
                break;
            default:
                // Reset / Replace / Move — rebuild controls without touching the subscription
                Canvas.Children.Clear();
                _nodeControls.Clear();
                if (Vm?.ActiveTab is not null)
                    foreach (var vm in Vm.ActiveTab.Nodes)
                        AddNodeControl(vm);
                break;
        }
        RefreshConnections();
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

        nodeVm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(NodeViewModel.X) or nameof(NodeViewModel.Y))
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

    private void OnNodePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not NodeControl ctrl || Vm is null) return;
        Log.Debug("Node ausgewählt: {0} ({1})", ctrl.ViewModel.Id, ctrl.ViewModel.ActionType);
        Vm.SelectedNode = ctrl.ViewModel;
        SelectWire(null);
        Focus();

        var pos = e.GetPosition(ctrl);
        var outPort = ctrl.OutputPortAt(pos);

        // Wire drag: pointer on a specific output port
        if (e.GetCurrentPoint(Canvas).Properties.IsLeftButtonPressed && outPort >= 0)
        {
            Canvas.BeginWireDrag(ctrl.ViewModel, outPort, e);
            e.Handled = true;
            return;
        }

        // Node drag: left button, not on port, not Alt
        if (e.GetCurrentPoint(Canvas).Properties.IsLeftButtonPressed
            && !e.KeyModifiers.HasFlag(KeyModifiers.Alt)
            && outPort < 0
            && !ctrl.IsOnInputPort(pos))
        {
            Canvas.BeginNodeDrag(ctrl.ViewModel, e.GetPosition(Canvas), e);
            e.Handled = true;
        }
    }

    private void OnWireDropped(object? sender, WireDropEventArgs e)
    {
        if (Vm is null) return;

        // Find node at drop position
        NodeControl? targetCtrl = null;
        foreach (var ctrl in _nodeControls.Values)
        {
            var nodePos = new Point(ctrl.ViewModel.X, ctrl.ViewModel.Y);
            var localPos = new Point(e.WorldPos.X - nodePos.X, e.WorldPos.Y - nodePos.Y);
            if (localPos.X >= -8 && localPos.X <= ctrl.ViewModel.Width + 8 &&
                localPos.Y >= -8 && localPos.Y <= ctrl.ViewModel.Height + 8)
            {
                if (ctrl.IsOnInputPort(localPos))
                {
                    targetCtrl = ctrl;
                    break;
                }
            }
        }

        if (targetCtrl is null) return;
        if (targetCtrl.ViewModel.Id == e.Source.Id) return;

        Log.Debug("Wire dropped: {0} → {1}", e.Source.Id, targetCtrl.ViewModel.Id);
        Vm.AddWire(e.Source.Id, e.OutputPort, targetCtrl.ViewModel.Id);
        RefreshConnections();
    }

    private void OnNodeDeleteRequested(object? sender, NodeViewModel vm)
    {
        Log.Info("Node löschen: {0} ({1})", vm.Id, vm.ActionType);
        Vm?.DeleteNode(vm);
        RebuildCanvas();
    }

    private void RefreshConnections()
    {
        if (Vm is null) return;
        ConnectionRenderer.Update(Vm.Wires, _nodeControls.Values.Select(c => c.ViewModel));
        ConnectionRenderer.InvalidateVisual();
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Vm is null) return;

        // Ignore clicks that originated on a node (handled in OnNodePointerPressed)
        var elem = e.Source as Avalonia.StyledElement;
        while (elem is not null)
        {
            if (elem is NodeControl) return;
            elem = elem.Parent;
        }

        Focus();
        var props = e.GetCurrentPoint(this).Properties;
        var canvasPos = e.GetPosition(Canvas);
        var hitWire = ConnectionRenderer.HitTest(Canvas.CanvasToWorld(canvasPos));

        if (props.IsRightButtonPressed)
        {
            if (hitWire is not null)
                ShowWireMenu(hitWire);
            else
                ShowAddNodeMenu(canvasPos);
            e.Handled = true;
        }
        else if (props.IsLeftButtonPressed)
        {
            SelectWire(hitWire);
            if (hitWire is not null) e.Handled = true;
        }
    }

    private void ShowWireMenu(WireViewModel wire)
    {
        SelectWire(wire);
        var menu = new ContextMenu();
        var del = new MenuItem { Header = "🗑 Verbindung löschen" };
        del.Click += (_, _) =>
        {
            Log.Info("Verbindung löschen: {0} → {1}", wire.SourceNodeId, wire.TargetNodeId);
            Vm?.RemoveWire(wire);
            SelectWire(null);
            RefreshConnections();
        };
        menu.Items.Add(del);
        menu.Open(this);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Delete or Key.Back)
        {
            if (_selectedWire is not null)
            {
                Log.Info("Verbindung löschen (Taste): {0} → {1}", _selectedWire.SourceNodeId, _selectedWire.TargetNodeId);
                Vm?.RemoveWire(_selectedWire);
                SelectWire(null);
                RefreshConnections();
                e.Handled = true;
            }
            else if (Vm?.SelectedNode is { } node)
            {
                Log.Info("Node löschen (Taste): {0} ({1})", node.Id, node.ActionType);
                Vm.DeleteNode(node);
                RebuildCanvas();
                e.Handled = true;
            }
        }
        base.OnKeyDown(e);
    }

    // ── Palette drag-and-drop ─────────────────────────────────────────────────

    private void OnDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = e.Data.Contains(NodePaletteView.NodeTypeFormat)
                     || e.Data.Contains(SubnodePanelView.SubnodeNameFormat)
            ? DragDropEffects.Copy
            : DragDropEffects.None;

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (Vm is null) return;
        var world = Canvas.CanvasToWorld(e.GetPosition(Canvas));

        // Subnode dragged from the list → create a call node targeting it.
        if (e.Data.Get(SubnodePanelView.SubnodeNameFormat) is string subnode && !string.IsNullOrEmpty(subnode))
        {
            Log.Info("Subnode per Drag&Drop einfügen: {0}", subnode);
            var vm = Vm.AddNode("call", world.X - 100, world.Y - 30);
            vm.Model.Config["target"] = subnode;
            vm.RaiseTitleChanged();
            return;
        }

        if (e.Data.Get(NodePaletteView.NodeTypeFormat) is not string type || string.IsNullOrEmpty(type)) return;
        Log.Info("Node per Drag&Drop hinzufügen: {0} @ ({1:F0},{2:F0})", type, world.X, world.Y);
        Vm.AddNode(type, world.X - 100, world.Y - 30); // center node on cursor
    }

    private void ShowAddNodeMenu(Point canvasPos)
    {
        var worldPos = Canvas.CanvasToWorld(canvasPos);
        Log.Debug("Kontext-Menü @ ({0:F0},{1:F0})", worldPos.X, worldPos.Y);
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
                    // AddNode raises CollectionChanged → OnNodesCollectionChanged renders the control.
                    Vm?.AddNode(d.Type, worldPos.X, worldPos.Y);
                };
                header.Items.Add(item);
            }
            menu.Items.Add(header);
        }

        menu.Open(this);
    }

    private void UpdateTabButtonStyles() { /* active-tab highlight handled by binding */ }

    // ── Tab bar interactions ──────────────────────────────────────────────────

    private void OnTabClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Control { Tag: FlowTabViewModel tab })
            Vm?.SwitchTab(tab);
    }

    private void OnTabClose(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Control { Tag: FlowTabViewModel tab })
            Vm?.CloseTab(tab);
    }
}
