using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using WebExStudio.AI;
using WebExStudio.Core.Models;

namespace WebExStudio.UI.Views;

public partial class SettingsWindow : Window
{
    private readonly RunConfig _config;
    private readonly AiOptions _ai;

    public bool Saved { get; private set; }

    public SettingsWindow() : this(new RunConfig(), new AiOptions()) { }

    public SettingsWindow(RunConfig config, AiOptions ai)
    {
        InitializeComponent();
        _config = config;
        _ai = ai;

        SelectCombo(BrowserBox, _config.Browser);
        SelectCombo(ChannelBox, _config.BrowserChannel);
        ExePathBox.Text = _config.BrowserExecutablePath;
        DriverPathBox.Text = _config.DriverPath;
        DownloadDirBox.Text = _config.DownloadDir;
        HeadlessBox.IsChecked = _config.Headless;

        ProxyServerBox.Text = _config.ProxyServer;
        ProxyBypassBox.Text = _config.ProxyBypass;
        ProxyUserBox.Text = _config.ProxyUsername;
        ProxyPassBox.Text = _config.ProxyPassword;

        SelectCombo(AiProviderBox, _ai.Provider);
        AiApiKeyBox.Text = _ai.ApiKey;
        AiModelBox.Text = _ai.Model;
        AiBaseUrlBox.Text = _ai.BaseUrl;
        AiSendHintsBox.IsChecked = _ai.SendHints;
        AiHintsBox.Text = _ai.Hints;
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

    private async void OnOpenDriverHelp(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is { } top)
            await top.Launcher.LaunchUriAsync(new Uri("https://playwright.dev/dotnet/docs/browsers"));
    }

    private async void OnBrowseDownload(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Download-Ordner wählen",
            AllowMultiple = false,
        });
        if (folders.Count > 0) DownloadDirBox.Text = folders[0].Path.LocalPath;
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        _config.Browser = (BrowserBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "chromium";
        _config.BrowserChannel = (ChannelBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
        _config.BrowserExecutablePath = ExePathBox.Text ?? string.Empty;
        _config.DriverPath = DriverPathBox.Text ?? string.Empty;
        _config.DownloadDir = DownloadDirBox.Text ?? string.Empty;
        _config.Headless = HeadlessBox.IsChecked == true;

        _config.ProxyServer = ProxyServerBox.Text ?? string.Empty;
        _config.ProxyBypass = ProxyBypassBox.Text ?? string.Empty;
        _config.ProxyUsername = ProxyUserBox.Text ?? string.Empty;
        _config.ProxyPassword = ProxyPassBox.Text ?? string.Empty;

        _ai.Provider = (AiProviderBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "anthropic";
        _ai.ApiKey = AiApiKeyBox.Text ?? string.Empty;
        _ai.Model = AiModelBox.Text ?? string.Empty;
        _ai.BaseUrl = AiBaseUrlBox.Text ?? string.Empty;
        _ai.SendHints = AiSendHintsBox.IsChecked == true;
        _ai.Hints = AiHintsBox.Text ?? string.Empty;

        Saved = true;
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
