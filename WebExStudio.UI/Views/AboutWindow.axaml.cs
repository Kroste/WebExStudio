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
        UpdateOpenReleaseButton.IsVisible = false;
        UpdateStatusText.Text = Loc.T("About_UpdateChecking");

        var result = await UpdateService.CheckForUpdateAsync(force);
        _releaseUrl = result.ReleaseUrl;

        if (result.Error is not null)
        {
            UpdateStatusText.Text = Loc.T("About_UpdateError");
        }
        else if (result.HasUpdate && result.Latest is not null)
        {
            UpdateStatusText.Text = string.Format(Loc.T("About_UpdateAvailable"), result.Latest);
            UpdateOpenReleaseButton.IsVisible = true;
        }
        else
        {
            UpdateStatusText.Text = Loc.T("About_UpdateCurrent");
        }
        UpdateCheckButton.IsEnabled = true;
    }
}
