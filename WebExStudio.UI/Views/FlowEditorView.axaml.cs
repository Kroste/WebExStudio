using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using NLog;
using WebExStudio.Core.Localization;
using WebExStudio.UI.Controls;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Views;

public partial class FlowEditorView : UserControl
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private FlowEditorViewModel? Vm => DataContext as FlowEditorViewModel;

    /// <summary>Doppelklick auf einen Tresor-Node → Anmeldedaten-Verwaltung öffnen.</summary>
    public event EventHandler? CredentialVaultRequested;

    private readonly Dictionary<string, NodeControl> _nodeControls = [];
    private readonly Dictionary<string, GroupControl> _groupControls = [];
    private WireViewModel? _selectedWire;

    public FlowEditorView()
    {
        InitializeComponent();
        Focusable = true;
        Canvas.GridOverlay = GridOverlay;
        Canvas.ConnectionRenderer = ConnectionRenderer;
        ConnectionRenderer.RenderTransform = Canvas.WorldTransform;
        // Gleicher Transform-Ursprung wie der Canvas, sonst driften Wires nach einem Resize.
        ConnectionRenderer.RenderTransformOrigin = Avalonia.RelativePoint.TopLeft;
        Canvas.WireDropped += OnWireDropped;
        Canvas.SelectionCompleted += OnSelectionCompleted;
        DataContextChanged += OnDataContextChanged;
        PointerPressed += OnCanvasPointerPressed;

        // Drop-Ziel ist die untransformierte Viewport-Fläche (CanvasArea), NICHT der gezoomte/
        // gescrollte NodeCanvas: dessen Trefferfläche ist nach Zoom/Scroll nur ein verschobenes
        // Band. Außerhalb davon kam Drag&Drop aus der Palette nicht mehr an (Ghost verschwand,
        // kein Drop). CanvasArea deckt immer das ganze Sichtfeld ab; die Drop-Position wird
        // ohnehin relativ zu CanvasArea gemessen und per CanvasToWorld umgerechnet.
        DragDrop.SetAllowDrop(CanvasArea, true);
        CanvasArea.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        CanvasArea.AddHandler(DragDrop.DropEvent, OnDrop);
        CanvasArea.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
    }

    /// <summary>Mausrad auf der untransformierten Viewport-Fläche → an den Canvas weiterreichen.</summary>
    private void OnCanvasAreaWheel(object? sender, PointerWheelEventArgs e)
    {
        Canvas.ApplyWheel(e.Delta, e.GetPosition(CanvasArea), e.KeyModifiers);
        e.Handled = true;
    }

    private void SelectWire(WireViewModel? wire)
    {
        _selectedWire = wire;
        ConnectionRenderer.SelectedWire = wire;
    }

    public void ResetView() => Canvas.ResetView();

    /// <summary>Öffnet den Tab des Nodes, wählt ihn aus und zentriert die Ansicht darauf.</summary>
    public void FocusNode(string nodeId)
    {
        if (Vm?.FocusNode(nodeId) is not { } node) return;
        Canvas.CenterOn(new Point(node.X + node.Width / 2, node.Y + node.Height / 2));
    }

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
        else if (e.PropertyName == nameof(FlowEditorViewModel.Groups))
            RebuildGroups();
    }

    private void OnSelectionCompleted(object? sender, Rect worldRect) => Vm?.SelectInRect(worldRect);

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
        _groupControls.Clear();

        if (Vm?.ActiveTab is null) return;

        Log.Debug("RebuildCanvas: Tab={0}, {1} Nodes", Vm.ActiveTab.Label, Vm.ActiveTab.Nodes.Count);

        foreach (var nodeVm in Vm.ActiveTab.Nodes)
            AddNodeControl(nodeVm);

        RebuildGroups();
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
                RepositionGroups();
            }
        };
    }

    private void OnNodePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not NodeControl ctrl || Vm is null) return;
        Log.Debug("Node ausgewählt: {0} ({1})", ctrl.ViewModel.Id, ctrl.ViewModel.ActionType);

        // Doppelklick auf den Tresor-Node öffnet die Anmeldedaten-Verwaltung.
        if (e.ClickCount == 2 && ctrl.ViewModel.ActionType == "credential_store")
        {
            CredentialVaultRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        // Doppelklick auf einen call-Node öffnet den referenzierten Subnode. Direkt hier
        // prüfen (statt über DoubleTapped), weil der Node-Drag den PointerPressed als Handled
        // markiert und so die Tap-Gesten-Erkennung unterdrückt.
        if (e.ClickCount == 2 && ctrl.ViewModel.ActionType == "call")
        {
            var target = ctrl.ViewModel.Model.Get("target");
            if (Vm.OpenSubnodeByName(target))
            {
                Log.Info("Subnode per Doppelklick öffnen: {0}", target);
                e.Handled = true;
                return;
            }
            Log.Warn("Doppelklick auf call-Node {0}: Subnode '{1}' nicht gefunden", ctrl.ViewModel.Id, target);
        }

        var ctrlKey = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (ctrlKey)
            Vm.SelectNode(ctrl.ViewModel, additive: true);
        else if (!Vm.SelectedNodes.Contains(ctrl.ViewModel))
            Vm.SelectNode(ctrl.ViewModel); // keep existing multi-selection when dragging a member
        else
            Vm.SelectedNode = ctrl.ViewModel; // primary follows the pressed node

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

        // Node drag: left button, not on port, not Alt — move the whole selection together.
        if (e.GetCurrentPoint(Canvas).Properties.IsLeftButtonPressed
            && !e.KeyModifiers.HasFlag(KeyModifiers.Alt)
            && outPort < 0
            && !ctrl.IsOnInputPort(pos))
        {
            Canvas.BeginNodeDrag(ctrl.ViewModel, Vm.SelectedNodes.ToList(), e.GetPosition(CanvasArea), e);
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
        Vm.PushUndo();
        Vm.AddWire(e.Source.Id, e.OutputPort, targetCtrl.ViewModel.Id);
        RefreshConnections();
    }

    private void OnNodeDeleteRequested(object? sender, NodeViewModel vm)
    {
        Log.Info("Node löschen: {0} ({1})", vm.Id, vm.ActionType);
        Vm?.PushUndo();
        Vm?.DeleteNode(vm);
        RebuildCanvas();
    }

    /// <summary>Öffnet die Node-Suche (z. B. vom Toolbar-Button).</summary>
    public void OpenSearch() => ShowSearch();

    /// <summary>Öffnet die Node-Suche (Strg+F) und springt zum gewählten Node.</summary>
    private async void ShowSearch()
    {
        if (Vm is null) return;
        var dlg = new NodeSearchDialog(Vm);
        if (TopLevel.GetTopLevel(this) is Window owner) await dlg.ShowDialog(owner);
        else dlg.Show();
        if (dlg.SelectedNodeId is { } id) FocusNode(id);
    }

    private void RefreshConnections()
    {
        if (Vm is null) return;
        ConnectionRenderer.Update(Vm.Wires, _nodeControls.Values.Select(c => c.ViewModel));
        ConnectionRenderer.InvalidateVisual();
    }

}
