using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using WebExStudio.UI.ViewModels;
using WebExStudio.UI.Views;

namespace WebExStudio.UI;

public partial class MainWindow : Window
{
    private MainWindowViewModel Vm => (MainWindowViewModel)DataContext!;

    public MainWindow() => InitializeComponent();

    // ── Custom title bar ──────────────────────────────────────────────────────

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e) =>
        ToggleMaximize();

    private void OnMinimize(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnToggleMaximize(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        ToggleMaximize();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseWindow(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Close();

    private async void OnAbout(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await new AboutWindow().ShowDialog(this);

    private async void OnSettings(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(Vm.RunConfig, Vm.AiOptions);
        await dlg.ShowDialog(this);
        if (dlg.Saved)
        {
            AppSettings.Save(Vm.RunConfig);
            AppSettings.SaveAi(Vm.AiOptions);
        }
    }

    private void OnNewFlow(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Vm.NewFlow();

    private async void OnGenerateFlow(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await new AiFlowDialog(Vm).ShowDialog(this);

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

    private bool _closing;

    private void OnRun(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Vm.StartRun();

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (!_closing)
        {
            _closing = true;

            // Cancel a running flow and give it a moment to release Playwright.
            if (Vm.CanStop)
            {
                e.Cancel = true;
                Vm.StopRun();
                if (Vm.RunTask is { } task)
                    await Task.WhenAny(task, Task.Delay(5000));
            }

            // Playwright spawns foreground driver/browser threads that keep the
            // process alive even after the window closes. Force-terminate to avoid
            // a lingering process that has to be killed manually.
            Environment.Exit(0);
            return;
        }
        base.OnClosing(e);
    }

    private void OnStop(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Vm.StopRun();

    private void OnResume(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Vm.Resume();

    private void OnFitView(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        (this.FindControl<FlowEditorView>("FlowEditorView"))?.FitToView();

    private void OnResetView(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        (this.FindControl<FlowEditorView>("FlowEditorView"))?.ResetView();
}
