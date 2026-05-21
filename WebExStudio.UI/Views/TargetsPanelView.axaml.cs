using Avalonia.Controls;
using WebExStudio.Core.Models;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Views;

public partial class TargetsPanelView : UserControl
{
    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    public TargetsPanelView() => InitializeComponent();

    private void OnAddTarget(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Vm is null) return;
        var config = new TargetConfig
        {
            Name = $"Target {Vm.Targets.Count + 1}",
            Host = "https://",
            ActionsFile = "actions/start.json",
            Enabled = true,
        };
        Vm.Targets.Add(new TargetViewModel(config));
        Vm.NotifyChanged(nameof(MainWindowViewModel.CanRun));
    }

    private void OnDeleteTarget(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.Tag is not TargetViewModel vm) return;
        Vm?.Targets.Remove(vm);
        Vm?.NotifyChanged(nameof(MainWindowViewModel.CanRun));
    }
}
