using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using WebExStudio.UI.Controls;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Views;

public partial class FlowEditorView : UserControl
{
    private FlowEditorViewModel? Vm => DataContext as FlowEditorViewModel;
    private readonly Dictionary<string, NodeControl> _nodeControls = [];

    public FlowEditorView()
    {
        InitializeComponent();
        Canvas.GridOverlay = GridOverlay;
        DataContextChanged += OnDataContextChanged;
        PointerPressed += OnCanvasPointerPressed;
    }

    public void ResetView() => Canvas.ResetView();

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (Vm is null) return;
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

        foreach (var nodeVm in Vm.Nodes)
            AddNodeControl(nodeVm);

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
                Avalonia.Controls.Canvas.SetLeft(ctrl, nodeVm.X);
                Avalonia.Controls.Canvas.SetTop(ctrl, nodeVm.Y);
                RefreshConnections();
            }
        };
    }

    private void OnNodePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not NodeControl ctrl || Vm is null) return;
        Vm.SelectedNode = ctrl.ViewModel;

        if (e.GetCurrentPoint(Canvas).Properties.IsLeftButtonPressed)
        {
            Canvas.BeginNodeDrag(ctrl.ViewModel, e.GetPosition(Canvas));
            e.Handled = true;
        }
    }

    private void OnNodeDeleteRequested(object? sender, NodeViewModel vm)
    {
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
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            ShowContextMenu(e.GetPosition(Canvas));
            e.Handled = true;
        }
    }

    private void ShowContextMenu(Point canvasPos)
    {
        var worldPos = Canvas.CanvasToWorld(canvasPos);
        var menu = new ContextMenu();

        foreach (var category in Core.Models.NodeCatalog.Categories)
        {
            var header = new MenuItem { Header = category };
            foreach (var def in Core.Models.NodeCatalog.GetByCategory(category))
            {
                var d = def;
                var item = new MenuItem { Header = $"{d.Icon}  {d.DisplayName}" };
                item.Click += (_, _) => Vm?.AddNode(d.Type, worldPos.X, worldPos.Y);
                header.Items.Add(item);
            }
            menu.Items.Add(header);
        }

        menu.Open(this);
    }
}
