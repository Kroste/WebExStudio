using Avalonia.Controls;
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
