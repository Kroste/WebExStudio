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
        MaximizedBox.IsChecked = _config.Maximized;
        SessionPersistBox.IsChecked = _config.SessionPersist;
        SessionFileBox.Text = _config.SessionFile;

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

    private async void OnBrowseSession(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Sitzungsdatei wählen",
            SuggestedFileName = "session.json",
            DefaultExtension = "json",
        });
        if (file is not null) SessionFileBox.Text = file.Path.LocalPath;
    }

    private async void OnDetectLocalAi(object? sender, RoutedEventArgs e)
    {
        DetectLocalAiButton.IsEnabled = false;
        LocalAiStatus.Text = "Suche lokale KI…";
        try
        {
            // Kein Proxy für localhost — sonst scheitert die Erkennung hinter einem System-Proxy.
            using var http = new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler { UseProxy = false })
            {
                Timeout = TimeSpan.FromSeconds(8),
            };
            var result = await LocalLlmDetector.DetectAsync(http);
            if (result is null)
            {
                LocalAiStatus.Text = "Keine lokale KI gefunden (Ollama / LM Studio / llama.cpp / Jan).";
                return;
            }

            SelectCombo(AiProviderBox, result.Provider);
            AiBaseUrlBox.Text = result.BaseUrl;
            if (result.Models.Count > 0 && string.IsNullOrWhiteSpace(AiModelBox.Text))
                AiModelBox.Text = result.Models[0];
            // OpenAI-kompatible lokale Server akzeptieren meist einen beliebigen Key.
            if (result.Provider == "openai" && string.IsNullOrWhiteSpace(AiApiKeyBox.Text))
                AiApiKeyBox.Text = "local";

            LocalAiStatus.Text = result.Models.Count > 0
                ? $"Gefunden: {result.Provider} @ {result.BaseUrl} — {result.Models.Count} Modell(e): {string.Join(", ", result.Models.Take(4))}"
                : $"Gefunden: {result.Provider} @ {result.BaseUrl} (keine Modelle installiert)";
        }
        catch (Exception ex)
        {
            LocalAiStatus.Text = $"Erkennung fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            DetectLocalAiButton.IsEnabled = true;
        }
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        _config.Browser = (BrowserBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "chromium";
        _config.BrowserChannel = (ChannelBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
        _config.BrowserExecutablePath = ExePathBox.Text ?? string.Empty;
        _config.DriverPath = DriverPathBox.Text ?? string.Empty;
        _config.DownloadDir = DownloadDirBox.Text ?? string.Empty;
        _config.Headless = HeadlessBox.IsChecked == true;
        _config.Maximized = MaximizedBox.IsChecked == true;
        _config.SessionPersist = SessionPersistBox.IsChecked == true;
        _config.SessionFile = SessionFileBox.Text ?? string.Empty;

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
