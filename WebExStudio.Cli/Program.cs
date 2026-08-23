using NLog;
using WebExStudio.Cli;
using WebExStudio.Core.Logging;

// Einstiegspunkt: die eigentliche Logik liegt in CliApp (besser testbar/lesbar).
// Logging und die Behandlung unbehandelter Ausnahmen werden hier aufgesetzt — die CLI führt
// dieselben Flows aus wie die GUI, ein Lauf per cron/CI ohne Logspur wäre nicht nachvollziehbar.
// Reihenfolge ist kritisch: der ${masked}-Renderer MUSS registriert sein, bevor NLog die
// Config parst — sonst verschluckt NLog den kompletten Message-Text (im Log steht nur "}}").
// Der [ModuleInitializer] in MaskedLayoutRenderer allein reicht hier NICHT: er läuft erst,
// wenn WebExStudio.Core zum ersten Mal berührt wird, und das wäre nach dem Config-Load.
// Er deckt die Prozesse ohne eigenes Main ab (Tests), dieser Aufruf den Start hier.
MaskedLayoutRenderer.Register();
LogManager.Setup().LoadConfigurationFromFile("NLog.config");
CliExceptionHandler.Register();

// Top-Level-Statements landen in einer namenlosen Program-Klasse; GetCurrentClassLogger() hieße
// also nur "Program" und würde von der Regel "WebExStudio.*" in der NLog.config nicht erfasst.
var log = LogManager.GetLogger("WebExStudio.Cli.Program");
log.Info("webex startet: {0}", string.Join(' ', args));
try
{
    var exitCode = await CliApp.Run(args);
    log.Info("webex beendet mit Exit-Code {0}", exitCode);
    return exitCode;
}
catch (Exception ex)
{
    CliExceptionHandler.LogFatal(ex, "Main", isTerminating: true);
    Console.Error.WriteLine("✗ Unerwarteter Fehler: " + ex.Message);
    return 1;
}
finally
{
    LogManager.Shutdown();
}
