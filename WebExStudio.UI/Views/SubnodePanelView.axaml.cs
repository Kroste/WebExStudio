using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Views;

public partial class SubnodePanelView : UserControl
{
    /// <summary>DataObject format carrying a subnode name when dragged onto the canvas.</summary>
    public const string SubnodeNameFormat = "webex/subnode-name";

    private MainWindowViewModel? MainVm => DataContext as MainWindowViewModel;
    private FlowEditorViewModel? Editor => MainVm?.FlowEditor;

    private FlowTabViewModel? _dragSubnode;
    private Point _dragStart;
    private bool _dragging;

    public SubnodePanelView()
    {
        InitializeComponent();
        SubnodeList.AddHandler(PointerPressedEvent, OnListPointerPressed, RoutingStrategies.Tunnel);
        SubnodeList.PointerMoved += OnListPointerMoved;
        SubnodeList.PointerReleased += OnListPointerReleased;
    }

    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (SubnodeList.SelectedItem is FlowTabViewModel tab)
            Editor?.OpenTab(tab);
    }

    // ── Drag a subnode onto the canvas → creates a call node ──────────────────

    private void OnListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(SubnodeList).Properties.IsLeftButtonPressed) return;
        var item = (e.Source as Control)?.DataContext as FlowTabViewModel;
        if (item is null) return;
        _dragSubnode = item;
        _dragStart = e.GetPosition(SubnodeList);
        _dragging = false;
    }

    private async void OnListPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragSubnode is null || _dragging) return;
        if (!e.GetCurrentPoint(SubnodeList).Properties.IsLeftButtonPressed) return;
        var p = e.GetPosition(SubnodeList);
        if (System.Math.Abs(p.X - _dragStart.X) < 4 && System.Math.Abs(p.Y - _dragStart.Y) < 4) return;

        _dragging = true;
        var data = new DataObject();
        data.Set(SubnodeNameFormat, _dragSubnode.Name ?? string.Empty);
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
        _dragSubnode = null;
    }

    private void OnListPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragSubnode = null;
        _dragging = false;
    }

    private async void OnNew(object? sender, RoutedEventArgs e)
    {
        if (Editor is null) return;
        var dlg = new SubnodeDialog("Neuer Subnode", "", "");
        await ShowDialogOverOwner(dlg);
        if (dlg.Confirmed)
            Editor.CreateSubnode(dlg.SubnodeName, dlg.SubnodeLabel);
    }

    private async void OnRename(object? sender, RoutedEventArgs e)
    {
        if (Editor is null || SubnodeList.SelectedItem is not FlowTabViewModel tab) return;
        var dlg = new SubnodeDialog("Subnode umbenennen", tab.Name ?? "", tab.Label);
        await ShowDialogOverOwner(dlg);
        if (dlg.Confirmed)
            Editor.RenameSubnode(tab, dlg.SubnodeName, dlg.SubnodeLabel);
    }

    private void OnDelete(object? sender, RoutedEventArgs e)
    {
        if (Editor is null || SubnodeList.SelectedItem is not FlowTabViewModel tab) return;
        Editor.DeleteSubnode(tab);
    }

    private async Task ShowDialogOverOwner(Window dlg)
    {
        if (TopLevel.GetTopLevel(this) is Window owner)
            await dlg.ShowDialog(owner);
        else
            dlg.Show();
    }
}
