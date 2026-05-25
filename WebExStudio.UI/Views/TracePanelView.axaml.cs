using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Views;

public partial class TracePanelView : UserControl
{
    private TracePanelViewModel? Vm => DataContext as TracePanelViewModel;

    public TracePanelView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SubscribeToEntries();
    }

    private static TraceEntryViewModel? EntryOf(object? sender) =>
        (sender as Control)?.DataContext as TraceEntryViewModel;

    private MainWindow? OwnerWindow => TopLevel.GetTopLevel(this) as MainWindow;

    private void OnEntryDoubleTapped(object? sender, TappedEventArgs e) => GoToNode(EntryOf(sender));

    private void OnGoToNode(object? sender, RoutedEventArgs e) => GoToNode(EntryOf(sender));

    private void GoToNode(TraceEntryViewModel? entry)
    {
        if (entry is null || string.IsNullOrEmpty(entry.NodeId)) return;
        OwnerWindow?.FocusNode(entry.NodeId);
    }

    private async void OnCopyEntry(object? sender, RoutedEventArgs e)
    {
        if (EntryOf(sender) is not { } entry || TopLevel.GetTopLevel(this)?.Clipboard is not { } clip) return;
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(DataFormat.Text, entry.CopyText));
        await clip.SetDataAsync(data);
    }

    private void OnSendToChat(object? sender, RoutedEventArgs e)
    {
        if (EntryOf(sender) is { } entry)
            OwnerWindow?.ShowChatWithText(entry.DiagnosticText);
    }

    private void SubscribeToEntries()
    {
        if (Vm is null) return;
        Vm.Entries.CollectionChanged += (_, _) =>
        {
            if (Vm.AutoScroll)
                Scroll.ScrollToEnd();
        };
    }

    private void OnClear(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Vm?.Clear();
}
