using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using WebExStudio.AI;
using WebExStudio.Core.Localization;
using WebExStudio.Core.Models;
using WebExStudio.Engine.Plugins;

namespace WebExStudio.UI.Views;

public partial class SettingsWindow : Window
{
    private readonly RunConfig _config;
    private readonly AiOptions _ai;
    private readonly HashSet<string> _disabledPlugins =
        new(AppSettings.LoadDisabledPlugins(), StringComparer.OrdinalIgnoreCase);

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

        BuildPluginList();
        BuildLanguageList();
    }

    // ── Sprache (Laufzeit-Umschaltung) ────────────────────────────────────────

    private sealed record LangItem(string Code, string Name)
    {
        public override string ToString() => Name; // Fallback, falls kein Template greift
    }

    private bool _languageReady;

    private void BuildLanguageList()
    {
        var loc = WebExStudio.Core.Localization.Loc.Instance;
        // Eigene Zeile: gezeichnete Landesflagge + Eigenname. Echte Flaggen-Emoji (🇩🇪 …) rendern auf
        // vielen Linux-Systemen nicht (Schrift ohne Regional-Indicator-Glyphen → leere Kästchen), darum
        // zeichnen wir die Flaggen als Vektor — überall identisch, ohne Schrift-/Bildabhängigkeit.
        LanguageBox.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<LangItem>(
            (item, _) =>
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                if (item is not null)
                {
                    row.Children.Add(BuildFlag(item.Code));
                    row.Children.Add(new TextBlock { Text = item.Name, VerticalAlignment = VerticalAlignment.Center });
                }
                return row;
            });

        LanguageBox.ItemsSource = loc.Languages.Select(c => new LangItem(c, loc.NameOf(c))).ToList();
        LanguageBox.SelectedItem = (LanguageBox.ItemsSource as IEnumerable<LangItem>)?
            .FirstOrDefault(i => i.Code == loc.Language);
        _languageReady = true; // erst danach reagiert OnLanguageChanged (nicht beim initialen Befüllen)
    }

    // ── Gezeichnete Landesflaggen (klein, ~22×15) ─────────────────────────────
    private const double FlagW = 22, FlagH = 15;

    private static Control BuildFlag(string code)
    {
        var canvas = new Canvas { Width = FlagW, Height = FlagH };
        switch (code)
        {
            case "de": StripesH(canvas, "#000000", "#DD0000", "#FFCE00"); break;
            case "ru": StripesH(canvas, "#FFFFFF", "#0039A6", "#D52B1E"); break;
            case "fr": StripesV(canvas, "#0055A4", "#FFFFFF", "#EF4135"); break;
            case "en": UnionJack(canvas); break;
            default:
                canvas.Children.Add(new Avalonia.Controls.Shapes.Rectangle
                { Width = FlagW, Height = FlagH, Fill = new SolidColorBrush(Color.Parse("#90A4AE")) });
                break;
        }
        return new Border
        {
            Width = FlagW, Height = FlagH,
            CornerRadius = new Avalonia.CornerRadius(2),
            ClipToBounds = true,
            BorderBrush = new SolidColorBrush(Color.Parse("#4A4A5A")),
            BorderThickness = new Avalonia.Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = canvas,
        };
    }

    private static void StripesH(Canvas c, string a, string b, string d)
    {
        double h = FlagH / 3.0;
        AddRect(c, 0, 0, FlagW, h + 1, a);
        AddRect(c, 0, h, FlagW, h + 1, b);
        AddRect(c, 0, 2 * h, FlagW, h, d);
    }

    private static void StripesV(Canvas c, string a, string b, string d)
    {
        double w = FlagW / 3.0;
        AddRect(c, 0, 0, w + 1, FlagH, a);
        AddRect(c, w, 0, w + 1, FlagH, b);
        AddRect(c, 2 * w, 0, w, FlagH, d);
    }

    private static void UnionJack(Canvas c)
    {
        AddRect(c, 0, 0, FlagW, FlagH, "#012169");          // blaues Feld
        AddLine(c, 0, 0, FlagW, FlagH, "#FFFFFF", 4);       // weißes Andreaskreuz
        AddLine(c, FlagW, 0, 0, FlagH, "#FFFFFF", 4);
        AddLine(c, 0, 0, FlagW, FlagH, "#C8102E", 2);       // rotes Andreaskreuz
        AddLine(c, FlagW, 0, 0, FlagH, "#C8102E", 2);
        AddLine(c, FlagW / 2, 0, FlagW / 2, FlagH, "#FFFFFF", 5); // weißes Georgskreuz
        AddLine(c, 0, FlagH / 2, FlagW, FlagH / 2, "#FFFFFF", 5);
        AddLine(c, FlagW / 2, 0, FlagW / 2, FlagH, "#C8102E", 3); // rotes Georgskreuz
        AddLine(c, 0, FlagH / 2, FlagW, FlagH / 2, "#C8102E", 3);
    }

    private static void AddRect(Canvas c, double x, double y, double w, double h, string color)
    {
        var r = new Avalonia.Controls.Shapes.Rectangle
        { Width = w, Height = h, Fill = new SolidColorBrush(Color.Parse(color)) };
        Canvas.SetLeft(r, x);
        Canvas.SetTop(r, y);
        c.Children.Add(r);
    }

    private static void AddLine(Canvas c, double x1, double y1, double x2, double y2, string color, double thick)
    {
        c.Children.Add(new Avalonia.Controls.Shapes.Line
        {
            StartPoint = new Avalonia.Point(x1, y1),
            EndPoint = new Avalonia.Point(x2, y2),
            Stroke = new SolidColorBrush(Color.Parse(color)),
            StrokeThickness = thick,
        });
    }

    private void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_languageReady || LanguageBox.SelectedItem is not LangItem item) return;
        WebExStudio.Core.Localization.Loc.Instance.SetLanguage(item.Code); // wirkt sofort
        AppSettings.SaveLanguage(item.Code);
    }

    // ── Plugins (zuvor im Über-Fenster) ───────────────────────────────────────

    private void BuildPluginList()
    {
        PluginDirsText.Text = Loc.T("Set_PluginDirsLabel") + " " + string.Join("  ·  ", AppSettings.PluginDirs);
        PluginsHost.Children.Clear();

        var plugins = NodePluginLoader.Plugins;
        if (plugins.Count == 0)
        {
            PluginsHost.Children.Add(new TextBlock
            {
                Text = Loc.T("Set_NoPlugins"),
                Foreground = new SolidColorBrush(Color.Parse("#90A4AE")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(2, 8, 2, 0),
            });
            return;
        }

        foreach (var p in plugins.OrderBy(p => p.File, StringComparer.OrdinalIgnoreCase))
        {
            var toggle = new CheckBox
            {
                IsChecked = !_disabledPlugins.Contains(p.File),
                VerticalAlignment = VerticalAlignment.Center,
            };
            ToolTip.SetTip(toggle, Loc.T("Set_PluginToggleTip"));
            var file = p.File;
            toggle.IsCheckedChanged += (_, _) =>
            {
                if (toggle.IsChecked == true) _disabledPlugins.Remove(file);
                else _disabledPlugins.Add(file);
                AppSettings.SaveDisabledPlugins([.. _disabledPlugins]);
            };

            var texts = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = p.File, Foreground = Brushes.White, FontSize = 12 },
                    new TextBlock { Text = p.Status, Foreground = new SolidColorBrush(Color.Parse("#78909C")), FontSize = 10 },
                },
            };

            PluginsHost.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.Parse("#16162A")),
                BorderBrush = new SolidColorBrush(Color.Parse("#2A2A4E")),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(6),
                Padding = new Avalonia.Thickness(10, 6),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { toggle, texts } },
            });
        }
    }

    private async void OnOpenPluginFolder(object? sender, RoutedEventArgs e)
    {
        // Konfig-Plugin-Ordner (zweiter Eintrag) anlegen und öffnen.
        var dir = AppSettings.PluginDirs.Last();
        System.IO.Directory.CreateDirectory(dir);
        if (TopLevel.GetTopLevel(this) is { } top)
            await top.Launcher.LaunchUriAsync(new Uri(dir));
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
