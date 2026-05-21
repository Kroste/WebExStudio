using Avalonia;
using Avalonia.ReactiveUI;
using NLog;

namespace WebExStudio.UI;

class Program
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    [STAThread]
    public static void Main(string[] args)
    {
        LogManager.Setup().LoadConfigurationFromFile("NLog.config");
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
            .UseReactiveUI()
            .WithInterFont()
            .LogToTrace();
}
