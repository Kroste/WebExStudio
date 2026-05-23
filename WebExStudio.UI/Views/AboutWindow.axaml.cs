using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DTM.Data.Terminal;
using DTM.Updater;

namespace DTM.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        // InformationalVersion enthält den vollen SDK-String (z.B. "1.0.2+abc123…").
        // Alles ab dem '+' (Git-Commit-Hash) wird abgeschnitten.
        var rawVer = Assembly.GetExecutingAssembly()
                             .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                             ?.InformationalVersion ?? "—";
        VersionText.Text = $"Version {rawVer.Split('+')[0]}";

        var logoUri = new Uri("avares://DTM/Assets/lhp_logo.png");
        if (AssetLoader.Exists(logoUri))
            LogoImage.Source = new Bitmap(AssetLoader.Open(logoUri));
        else
            LogoImage.IsVisible = false;
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnTitleClose(object? _, RoutedEventArgs e) => Close();

    private async void OnCheckUpdate(object? _, RoutedEventArgs e)
    {
        UpdateCheckButton.IsEnabled = false;
        UpdateStatusText.Text = "Prüfe auf Updates …";

        string src = FocSqlRuntime.Current.UpdateSource;
        if (string.IsNullOrWhiteSpace(src))
        {
            UpdateStatusText.Text = "Update-Quelle nicht konfiguriert.";
            UpdateCheckButton.IsEnabled = true;
            return;
        }

        try
        {
            Version? newVersion = await UpdateService.CheckForUpdateAsync(src);
            if (newVersion is null)
            {
                UpdateStatusText.Text = "Aktuell — keine neue Version verfügbar.";
                UpdateCheckButton.IsEnabled = true;
                return;
            }

            // Update gefunden → UpdatePromptWindow öffnen
            var dlg = new UpdatePromptWindow(newVersion.ToString(), UpdateService.CurrentVersion().ToString(3));
            await dlg.ShowDialog(this);

            if (dlg.Result == UpdateDialogResult.ApplyNow)
            {
                UpdateCheckButton.IsEnabled = false;
                UpdateStatusText.Text = "Update wird kopiert …";
                var applyProgress = new Progress<(int Done, int Total, string File)>(p =>
                    UpdateStatusText.Text = $"Kopiere {p.Done}/{p.Total}: {p.File}");
                await UpdateService.ApplyUpdateAsync(src, applyProgress);
            }
            else if (dlg.Result == UpdateDialogResult.Later)
            {
                UpdateStatusText.Text = $"Update auf {newVersion} wird später erinnert.";
                UpdateCheckButton.IsEnabled = true;
            }
            else
            {
                UpdateStatusText.Text = $"Update auf {newVersion} übersprungen.";
                UpdateCheckButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = $"Fehler: {ex.Message}";
            UpdateCheckButton.IsEnabled = true;
        }
    }
}
