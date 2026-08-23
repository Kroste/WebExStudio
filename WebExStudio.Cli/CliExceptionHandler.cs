using NLog;

namespace WebExStudio.Cli;

/// <summary>
/// Zentrale Behandlung unbehandelter Ausnahmen im Headless-Runner: NLog-Fatal statt stillem
/// Abbruch. Gegenstück zum <c>GlobalExceptionHandler</c> der GUI — nur ohne Fehlerdialog, denn
/// hier gibt es kein Fenster. Der Nutzer sieht die Kurzfassung auf stderr, die vollständige
/// Ausnahme samt Stacktrace steht im Log.
/// </summary>
public static class CliExceptionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public static void Register()
    {
        // Letzte Instanz: Prozess stirbt gleich — mindestens noch loggen und flushen.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogFatal(e.ExceptionObject as Exception, "AppDomain", e.IsTerminating);

        // Vergessene/verwaiste Tasks: loggen, Prozess aber nicht beenden.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogFatal(e.Exception, "TaskScheduler (unbeobachteter Task)", isTerminating: false);
            e.SetObserved();
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
}
