using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using WebExStudio.Core.Models;

namespace WebExStudio.UI.Views;

public partial class SettingsWindow : Window
{
    private readonly RunConfig _config;

    public bool Saved { get; private set; }

    public SettingsWindow() : this(new RunConfig()) { }

    public SettingsWindow(RunConfig config)
    {
        InitializeComponent();
        _config = config;

        SelectCombo(BrowserBox, _config.Browser);
        SelectCombo(ChannelBox, _config.BrowserChannel);
        ExePathBox.Text = _config.BrowserExecutablePath;
        DriverPathBox.Text = _config.DriverPath;
        HeadlessBox.IsChecked = _config.Headless;
    }

    private static void SelectCombo(ComboBox box, string value)
    {
        foreach (var item in box.Items)
        {
            if (item is ComboBoxItem cbi && string.Equals(ItemText(cbi), value, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = item;
                return;
            }
        }
        box.SelectedIndex = 0;
    }

    private static string ItemText(ComboBoxItem item) => item.Content?.ToString() ?? string.Empty;

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private async void OnBrowseExe(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Browser-Programm wählen",
            AllowMultiple = false,
        });
        if (files.Count > 0) ExePathBox.Text = files[0].Path.LocalPath;
    }

    private async void OnBrowseDriver(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Treiber-Ordner wählen",
            AllowMultiple = false,
        });
        if (folders.Count > 0) DriverPathBox.Text = folders[0].Path.LocalPath;
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        _config.Browser = (BrowserBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "chromium";
        _config.BrowserChannel = (ChannelBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
        _config.BrowserExecutablePath = ExePathBox.Text ?? string.Empty;
        _config.DriverPath = DriverPathBox.Text ?? string.Empty;
        _config.Headless = HeadlessBox.IsChecked == true;
        Saved = true;
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
