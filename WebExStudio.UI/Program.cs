using Avalonia;
using ReactiveUI.Avalonia;
using NLog;
using WebExStudio.Core.Serialization;

namespace WebExStudio.UI;

class Program
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    [STAThread]
    public static void Main(string[] args)
    {
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
