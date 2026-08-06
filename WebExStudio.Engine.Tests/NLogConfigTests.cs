using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;
using Xunit;

namespace WebExStudio.Engine.Tests;

/// <summary>
/// Schützt die ausgelieferte NLog.config vor Parse-Fehlern (z. B. unescapte Doppelpunkte in
/// Layouts), die das Laden der gesamten Konfiguration verhindern → keine Logs mehr.
/// </summary>
public class NLogConfigTests
{
    private static string FindConfig()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "WebExStudio.UI", "NLog.config");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("WebExStudio.UI/NLog.config nicht gefunden.");
    }

    [Fact]
    public void NLogConfig_LoadsAndRendersAllLayouts()
    {
        LogManager.ThrowConfigExceptions = true;
        try
        {
            // Die ausgelieferte Config nutzt ${masked} — der Renderer muss registriert sein,
            // sonst wirft das Laden "unknown type-alias 'masked'" (wie zur Laufzeit in Program.cs).
            WebExStudio.Core.Logging.MaskedLayoutRenderer.Register();
            var config = new XmlLoggingConfiguration(FindConfig());
            Assert.NotEmpty(config.AllTargets);

            // Rendern erzwingt das Parsen der Layouts — wirft bei fehlerhaften Layouts.
            var ev = new LogEventInfo(LogLevel.Error, "WebExStudio.Test", "Testmeldung")
            {
                Exception = new InvalidOperationException("boom"),
            };
            foreach (var target in config.AllTargets.OfType<TargetWithLayout>())
                _ = target.Layout.Render(ev);
        }
        finally
        {
            LogManager.ThrowConfigExceptions = false;
        }
    }
}
