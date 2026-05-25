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
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
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
            .UseReactiveUI(_ => { })
            .WithInterFont()
            .LogToTrace();
}
