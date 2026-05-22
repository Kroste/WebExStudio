using Avalonia.Controls;
using Avalonia.Platform.Storage;
using WebExStudio.UI.ViewModels;
using WebExStudio.UI.Views;

namespace WebExStudio.UI;

public partial class MainWindow : Window
{
    private MainWindowViewModel Vm => (MainWindowViewModel)DataContext!;

    public MainWindow() => InitializeComponent();

    private async void OnOpenProject(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Projektordner öffnen",
            AllowMultiple = false,
        });
        if (folders.Count > 0)
            await Vm.OpenProjectAsync(folders[0].Path.LocalPath);
    }

    private async void OnOpenFlow(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Flow-Datei öffnen",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Flow JSON") { Patterns = ["*.json"] }],
        });
        if (files.Count > 0)
            await Vm.OpenFlowAsync(files[0].Path.LocalPath);
    }

    private async void OnSaveFlow(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Vm.FlowEditor.Document?.FilePath is { } existing)
        {
            await Vm.SaveFlowAsync(existing);
            return;
        }
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Flow speichern",
            DefaultExtension = "json",
            FileTypeChoices = [new FilePickerFileType("Flow JSON") { Patterns = ["*.json"] }],
        });
        if (file is not null)
            await Vm.SaveFlowAsync(file.Path.LocalPath);
    }

    private async void OnRun(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await Vm.RunAsync();

    private void OnStop(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Vm.StopRun();

    private void OnFitView(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        (this.FindControl<FlowEditorView>("FlowEditorView"))?.FitToView();

    private void OnResetView(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        (this.FindControl<FlowEditorView>("FlowEditorView"))?.ResetView();
}
