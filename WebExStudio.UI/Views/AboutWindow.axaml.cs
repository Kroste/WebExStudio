using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WebExStudio.Core.Localization;

namespace WebExStudio.UI.Views;

public partial class AboutWindow : Window
{
    private readonly System.Action<string>? _onLoadExample;
    private string _releaseUrl = UpdateService.ReleasePageUrl;
    private UpdateService.UpdateResult? _update;

    public AboutWindow() : this(null) { }

    public AboutWindow(System.Action<string>? onLoadExample)
    {
        _onLoadExample = onLoadExample;
        InitializeComponent();

        var rawVer = Assembly.GetExecutingAssembly()
                             .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                             ?.InformationalVersion ?? "—";
        VersionText.Text = string.Format(Loc.T("About_Version"), rawVer.Split('+')[0]);

        // Bei Öffnen einen (nicht blockierenden) Update-Check anstoßen; Ergebnis ist gecacht.
        Opened += async (_, _) => await RunUpdateCheckAsync(force: false);
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnTitleClose(object? _, RoutedEventArgs e) => Close();

    private async void OnHelp(object? _, RoutedEventArgs e) => await new HelpWindow(_onLoadExample).ShowDialog(this);

    /// <summary>Öffnet die Buy-Me-a-Coffee-Seite von Kroste im Standardbrowser
    /// (plattformneutral via Avalonia-Launcher: Windows ShellExecute, Linux xdg-open, macOS open).</summary>
    private async void OnCoffee(object? _, RoutedEventArgs e)
    {
        var top = GetTopLevel(this);
        if (top is not null)
            await top.Launcher.LaunchUriAsync(new System.Uri("https://buymeacoffee.com/kroste"));
    }

    private async void OnCheckUpdate(object? _, RoutedEventArgs e) => await RunUpdateCheckAsync(force: true);

    private async void OnOpenRelease(object? _, RoutedEventArgs e)
    {
        var top = GetTopLevel(this);
        if (top is not null)
            await top.Launcher.LaunchUriAsync(new System.Uri(_releaseUrl));
    }

    private async Task RunUpdateCheckAsync(bool force)
    {
        UpdateCheckButton.IsEnabled = false;
        UpdateInstallButton.IsVisible = false;
        UpdateOpenReleaseButton.IsVisible = false;
        UpdateStatusText.Text = Loc.T("About_UpdateChecking");

        var result = await UpdateService.CheckForUpdateAsync(force);
        _update = result;
        _releaseUrl = result.ReleaseUrl;

        if (result.Error is not null)
        {
            UpdateStatusText.Text = Loc.T("About_UpdateError");
        }
        else if (result.HasUpdate && result.Latest is not null)
        {
            UpdateStatusText.Text = string.Format(Loc.T("About_UpdateAvailable"), result.Latest);
            if (result.CanSelfUpdate)
            {
                // Installierbar: „⬇ Update installieren" als Primäraktion, Prüf-Button ausblenden.
                UpdateInstallButton.IsVisible = true;
                UpdateCheckButton.IsVisible = false;
            }
            // Release-Seite bleibt als sekundäre Option immer erreichbar.
            UpdateOpenReleaseButton.IsVisible = true;
        }
        else
        {
            UpdateStatusText.Text = Loc.T("About_UpdateCurrent");
        }
        UpdateCheckButton.IsEnabled = true;
    }

    /// <summary>
    /// Echtes Self-Update: lädt das Asset (mit Fortschritt), startet den Austausch-Prozess und
    /// beendet die App. Der explizite Klick auf diesen Button ist die Zustimmung (kein Silent-Install).
    /// </summary>
    private async void OnInstallUpdate(object? _, RoutedEventArgs e)
    {
        if (_update is null || !_update.CanSelfUpdate) return;

        UpdateInstallButton.IsEnabled = false;
        UpdateCheckButton.IsEnabled = false;
        UpdateOpenReleaseButton.IsEnabled = false;
        UpdateProgress.IsVisible = true;
        UpdateProgress.Value = 0;
        UpdateStatusText.Text = Loc.T("About_UpdateDownloading");

        var progress = new System.Progress<double>(p => UpdateProgress.Value = p * 100);
        var ok = await UpdateService.DownloadAndApplyAsync(_update, progress);
        if (ok)
        {
            // Pflicht: App beenden, sonst wartet der Installer ewig auf das Prozessende.
            UpdateStatusText.Text = Loc.T("About_UpdateRestarting");
            UpdateService.TerminateForUpdate();
            return;
        }

        // Fehlgeschlagen (Download/Proxy/Asset): zurück in den bedienbaren Zustand.
        UpdateStatusText.Text = Loc.T("About_UpdateError");
        UpdateProgress.IsVisible = false;
        UpdateInstallButton.IsEnabled = true;
        UpdateCheckButton.IsEnabled = true;
        UpdateOpenReleaseButton.IsEnabled = true;
    }
}
