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

    public FlowEditorView()
    {
        InitializeComponent();
        Canvas.GridOverlay = GridOverlay;
        Canvas.ConnectionRenderer = ConnectionRenderer;
        ConnectionRenderer.RenderTransform = Canvas.WorldTransform;
        Canvas.WireDropped += OnWireDropped;
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

    private void RebuildCanvas()
    {
        Canvas.Children.Clear();
        _nodeControls.Clear();

        if (Vm?.ActiveTab is null) return;

        Log.Debug("RebuildCanvas: Tab={0}, {1} Nodes", Vm.ActiveTab.Label, Vm.ActiveTab.Nodes.Count);

        foreach (var nodeVm in Vm.ActiveTab.Nodes)
            AddNodeControl(nodeVm);

        RefreshConnections();

        // Rebuild tab bar button styles
        UpdateTabButtonStyles();
    }

    private void AddNodeControl(NodeViewModel nodeVm)
    {
        var ctrl = new NodeControl(nodeVm);
        ctrl.PointerPressed += OnNodePointerPressed;
        ctrl.DeleteRequested += OnNodeDeleteRequested;
        ctrl.OpenSubTabRequested += OnNodeOpenSubTabRequested;
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

        var pos = e.GetPosition(ctrl);

        // Wire drag: pointer on output port
        if (e.GetCurrentPoint(Canvas).Properties.IsLeftButtonPressed
            && ctrl.IsOnOutputPort(pos)
            && ctrl.ViewModel.Definition.OutputPorts > 0)
        {
            Canvas.BeginWireDrag(ctrl.ViewModel, 0, e);
            e.Handled = true;
            return;
        }

        // Node drag: left button, not on port, not Alt
        if (e.GetCurrentPoint(Canvas).Properties.IsLeftButtonPressed
            && !e.KeyModifiers.HasFlag(KeyModifiers.Alt)
            && !ctrl.IsOnOutputPort(pos)
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

    private void OnNodeOpenSubTabRequested(object? sender, (NodeViewModel vm, string slot) args)
    {
        Log.Debug("Sub-Tab öffnen: {0} slot={1}", args.vm.Id, args.slot);
        Vm?.OpenSubTab(args.vm, args.slot);
        // Tab switch triggers RebuildCanvas via PropertyChanged
    }

    private void RefreshConnections()
    {
        if (Vm is null) return;
        ConnectionRenderer.Update(Vm.Wires, _nodeControls.Values.Select(c => c.ViewModel));
        ConnectionRenderer.InvalidateVisual();
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed) return;

        var elem = e.Source as Avalonia.StyledElement;
        while (elem is not null)
        {
            if (elem is NodeControl) return;
            elem = elem.Parent;
        }

        ShowAddNodeMenu(e.GetPosition(Canvas));
        e.Handled = true;
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
                    var vm = Vm?.AddNode(d.Type, worldPos.X, worldPos.Y);
                    if (vm is not null)
                    {
                        AddNodeControl(vm);
                        RefreshConnections();
                    }
                };
                header.Items.Add(item);
            }
            menu.Items.Add(header);
        }

        menu.Open(this);
    }

    // ── Tab bar interactions ──────────────────────────────────────────────────

    private void UpdateTabButtonStyles()
    {
        // Find the ItemsControl for tabs and update button styles
        var tabBar = this.FindControl<ItemsControl>("TabBar");
        if (tabBar is null) return;
        // Styling is done via VM binding — the active tab can be highlighted via a converter
        // For now, just update which tab is visually active (handled by DataContext binding)
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // Wire up tab button click handlers after the AXAML items are realized
        WireTabButtons();
    }

    private void WireTabButtons()
    {
        var tabBar = this.FindControl<ItemsControl>("TabBar");
        if (tabBar is null) return;

        // Subscribe to tab button clicks via pointer events on the ItemsControl
        tabBar.PointerPressed += OnTabBarPointerPressed;
    }

    private void OnTabBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Vm is null) return;
        var elem = e.Source as Avalonia.StyledElement;
        while (elem is not null)
        {
            if (elem is Button btn && btn.DataContext is FlowTabViewModel tabVm)
            {
                Vm.SwitchTab(tabVm);
                e.Handled = true;
                return;
            }
            elem = elem.Parent;
        }
    }
}
