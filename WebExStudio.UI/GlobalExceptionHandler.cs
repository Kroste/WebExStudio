using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using NLog;

namespace WebExStudio.UI;

/// <summary>
/// Zentrale Behandlung unbehandelter Ausnahmen: NLog-Fatal + Fehlerdialog.
///
/// Fängt insbesondere Ausnahmen aus async-void-Eventhandlern ab (landen im
/// Dispatcher), die sonst die App kommentarlos und ohne Logeintrag beenden
/// würden. Wird einmalig in <c>Program.Main</c> registriert.
/// </summary>
public static class GlobalExceptionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public static void Register()
    {
        // Letzte Instanz: Prozess stirbt gleich — mindestens noch loggen und flushen.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogFatal(e.ExceptionObject as Exception, "AppDomain", e.IsTerminating);

        // Vergessene/verwaiste Tasks: loggen, aber App nicht beenden.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogFatal(e.Exception, "TaskScheduler (unbeobachteter Task)", isTerminating: false);
            e.SetObserved();
        };

        // UI-Thread (inkl. async-void-Eventhandler): loggen, Dialog zeigen, App am Leben halten.
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            LogFatal(e.Exception, "UI-Thread", isTerminating: false);
            e.Handled = true;
            ShowErrorDialog(e.Exception);
        };
    }

    /// <summary>Schreibt einen Fatal-Eintrag. Wirft selbst niemals (Endlosschleifen-Schutz).</summary>
    public static void LogFatal(Exception? ex, string source, bool isTerminating)
    {
        try
        {
            Log.Fatal(ex, "Unbehandelte Ausnahme ({Source}, terminating={Terminating})", source, isTerminating);
            if (isTerminating)
                LogManager.Flush(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // bewusst leer: der Fehlerpfad darf keinen Folgefehler erzeugen
        }
    }

    private static void ShowErrorDialog(Exception ex)
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
                || desktop.MainWindow is null)
                return;

            var text = new SelectableTextBlock
            {
                Text = Text.WrapSafeText.Sanitize($"{ex.GetType().Name}: {ex.Message}"),
                TextWrapping = TextWrapping.Wrap,
            };
            var hint = new TextBlock
            {
                Text = "Details stehen im Log (logs/error.log).",
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
            };
            var ok = new Button
            {
                Content = "OK",
                HorizontalAlignment = HorizontalAlignment.Right,
                MinWidth = 90,
            };
            var win = new Window
            {
                Title = "Unerwarteter Fehler",
                Width = 520,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 12,
                    Children = { text, hint, ok },
                },
            };
            ok.Click += (_, _) => win.Close();
            _ = win.ShowDialog(desktop.MainWindow);
        }
        catch
        {
            // Dialog ist Komfort — wenn selbst der scheitert, bleibt der Logeintrag.
        }
    }
}
