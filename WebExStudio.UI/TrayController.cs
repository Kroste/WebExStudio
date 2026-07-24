using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using NLog;
using WebExStudio.Core.Localization;

namespace WebExStudio.UI;

/// <summary>
/// System-Tray-Anbindung nach Kroste-Muster: <b>Minimieren legt ins Tray, Schließen beendet regulär</b>.
///
/// <para>Vier Fallen (alle abgesichert):</para>
/// <list type="bullet">
///   <item>GC-Referenz halten — die App muss diesen Controller als Feld halten, sonst räumt der GC
///     das TrayIcon irgendwann still weg. → Instanz in <see cref="App"/> aufbewahren.</item>
///   <item>Restore-Guard — Wiederherstellen setzt <c>WindowState.Normal</c>, was denselben Listener
///     erneut feuert. Ohne Flag entsteht eine Minimize/Restore-Schleife.</item>
///   <item>try/catch mit Fallback — auf Headless-Servern oder mit kaputtem DBus kann der Tray-Setup
///     werfen; dann verhält sich Minimieren normal, die App bleibt nutzbar.</item>
///   <item>Linux zieht <c>Tmds.DBus.Protocol</c> transitiv über Avalonia (kein manueller Pin nötig,
///     solange Avalonia die aktuelle Version aussteuert).</item>
/// </list>
/// </summary>
public sealed class TrayController
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
    private readonly Window _window;
    private TrayIcon? _tray;
    private bool _restoreInProgress;

    public TrayController(Application app, IClassicDesktopStyleApplicationLifetime lifetime, Window window)
    {
        _lifetime = lifetime;
        _window = window;

        try
        {
            var icon = LoadIcon();
            var menu = new NativeMenu();
            var showItem = new NativeMenuItem(Loc.T("Tray_Show"));
            showItem.Click += (_, _) => Restore();
            menu.Add(showItem);
            menu.Add(new NativeMenuItemSeparator());
            var quitItem = new NativeMenuItem(Loc.T("Tray_Quit"));
            quitItem.Click += (_, _) => Dispatcher.UIThread.Post(() => _lifetime.Shutdown());
            menu.Add(quitItem);

            _tray = new TrayIcon
            {
                Icon = icon,
                ToolTipText = "WebExStudio",
                Menu = menu,
                IsVisible = true,
            };
            _tray.Clicked += (_, _) => Restore();
            TrayIcon.SetIcons(app, new TrayIcons { _tray });

            _window.PropertyChanged += OnWindowPropertyChanged;
            Log.Info("Tray initialisiert.");
        }
        catch (Exception ex)
        {
            // Kein Tray verfügbar (Headless-Server, kaputtes DBus, Wayland ohne Tray-Support):
            // Fenster verhält sich beim Minimieren normal, die App bleibt nutzbar.
            Log.Warn(ex, "Tray konnte nicht initialisiert werden — Minimieren fällt auf Standardverhalten zurück.");
            _tray = null;
        }
    }

    private static WindowIcon? LoadIcon()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://WebExStudio/Assets/webexstudio.png"));
            return new WindowIcon(stream);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "App-Icon für Tray konnte nicht geladen werden.");
            return null;
        }
    }

    /// <summary>Minimieren → Fenster verstecken (nicht schließen — Schließen bleibt „App beenden").</summary>
    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_tray is null) return;
        if (e.Property != Window.WindowStateProperty) return;
        if (_restoreInProgress) return;
        if (_window.WindowState != WindowState.Minimized) return;

        Log.Debug("Fenster minimiert → in den Tray verstecken.");
        _window.Hide();
    }

    /// <summary>Fenster wieder hervorholen (Tray-Klick oder „Anzeigen").</summary>
    public void Restore()
    {
        _restoreInProgress = true;
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                _window.Show();
                if (_window.WindowState == WindowState.Minimized)
                    _window.WindowState = WindowState.Normal;
                _window.Activate();
            }
            catch (Exception ex)
            {
                Log.Warn(ex, "Fenster konnte nicht aus dem Tray zurückgeholt werden.");
            }
            finally
            {
                _restoreInProgress = false;
            }
        });
    }
}
