using Avalonia;
using ReactiveUI.Avalonia;
using NLog;
using WebExStudio.Core.Logging;
using WebExStudio.Core.Serialization;

namespace WebExStudio.UI;

class Program
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    [STAThread]
    public static void Main(string[] args)
    {
        // ${masked}-Renderer VOR dem Laden der Config registrieren, sonst wirft NLog beim
        // Parsen der maskierten Layouts "unknown type-alias 'masked'".
        // Der ${masked}-Renderer registriert sich per [ModuleInitializer] beim Laden von
        // WebExStudio.Core — hier ist deshalb kein Aufruf mehr nötig (und wäre auch zu spät
        // für Prozesse ohne dieses Main, siehe MaskedLayoutRenderer).
        // Reihenfolge ist kritisch: der ${masked}-Renderer MUSS registriert sein, bevor NLog die
        // Config parst — sonst verschluckt NLog den kompletten Message-Text (im Log steht nur "}}").
        // Der [ModuleInitializer] in MaskedLayoutRenderer allein reicht hier NICHT: er läuft erst,
        // wenn WebExStudio.Core zum ersten Mal berührt wird, und das wäre nach dem Config-Load.
        // Er deckt die Prozesse ohne eigenes Main ab (Tests), dieser Aufruf den Start hier.
        MaskedLayoutRenderer.Register();
        LogManager.Setup().LoadConfigurationFromFile("NLog.config");

        // Headless legacy-project converter: --convert <legacyProjectDir> <outFile.json>
        if (args.Length >= 3 && args[0] == "--convert")
        {
            var doc = LegacyImporter.Convert(args[1]);
            FlowSerializer2.SaveAsync(doc, args[2]).GetAwaiter().GetResult();
            Console.WriteLine($"Konvertiert: {doc.Tabs.Count} Tabs, {doc.Nodes.Count} Nodes → {args[2]}");
            LogManager.Shutdown();
            return;
        }

        Log.Info("WebExStudio startet");
        GlobalExceptionHandler.Register();
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // Absturz beim Start oder im Hauptloop: Ursache loggen, dann weiterreichen.
            GlobalExceptionHandler.LogFatal(ex, "Main", isTerminating: true);
            throw;
        }
        finally
        {
            Log.Info("WebExStudio beendet");
            LogManager.Shutdown();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // Feste Fenster-Identität (WM_CLASS) unter Linux/X11 (auch XWayland): unabhängig vom
            // Prozessnamen (der sich bei AppImage-/Single-File-Extraktion ändern kann), damit
            // KWin-Fensterregeln und Taskleisten-Zuordnung überall gleich greifen.
            .With(new X11PlatformOptions { WmClass = "WebExStudio" })
            .UseReactiveUI(_ => { })
            .WithInterFont()
            .LogToTrace();
}
