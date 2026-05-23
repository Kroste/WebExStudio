using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Views;

public partial class SubnodePanelView : UserControl
{
    private MainWindowViewModel? MainVm => DataContext as MainWindowViewModel;
    private FlowEditorViewModel? Editor => MainVm?.FlowEditor;

    public SubnodePanelView() => InitializeComponent();

    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (SubnodeList.SelectedItem is FlowTabViewModel tab)
            Editor?.OpenTab(tab);
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
