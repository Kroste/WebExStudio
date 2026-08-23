using System.Runtime.CompilerServices;
using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;
using WebExStudio.Core.Logging;
using Xunit;

namespace WebExStudio.Engine.Tests;

/// <summary>
/// Schützt die ausgelieferten NLog-Konfigurationen (GUI + CLI) vor Parse-Fehlern (z. B. unescapte
/// Doppelpunkte in Layouts), die das Laden der gesamten Konfiguration verhindern → keine Logs mehr.
/// Prüft zusätzlich, dass die Maskierung wirklich greift und nicht nur im Layout steht.
/// </summary>
public class NLogConfigTests
{
    static NLogConfigTests()
    {
        // Der ${masked}-Renderer registriert sich per [ModuleInitializer] beim Laden von
        // WebExStudio.Core. Ein bloßes typeof(...) löst den Modulkonstruktor NICHT aus — deshalb
        // hier explizit erzwingen. Ohne registrierten Renderer scheitert das Laden der Config mit
        // "unknown type-alias 'masked'".
        RuntimeHelpers.RunModuleConstructor(typeof(MaskedLayoutRenderer).Module.ModuleHandle);
    }

    private static string FindConfig(string project)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, project, "NLog.config");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"{project}/NLog.config nicht gefunden.");
    }

    [Theory]
    [InlineData("WebExStudio.UI")]
    [InlineData("WebExStudio.Cli")]
    public void NLogConfig_LaedtUndRendertAlleLayouts(string project)
    {
        LogManager.ThrowConfigExceptions = true;
        try
        {
            var config = new XmlLoggingConfiguration(FindConfig(project));
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

    [Theory]
    [InlineData("WebExStudio.UI")]
    [InlineData("WebExStudio.Cli")]
    public void NLogConfig_MaskiertGeheimnisseWirklich(string project)
    {
        // Bewusst NICHT gegen den Layout-String prüfen ("enthält 'masked'"): ein ${masked} über den
        // Umweg einer <variable> lädt fehlerfrei und maskiert trotzdem nichts. Nur ein echtes
        // Render-Ergebnis beweist, dass die Schutzschicht greift.
        var config = new XmlLoggingConfiguration(FindConfig(project));
        var ev = new LogEventInfo(LogLevel.Error, "WebExStudio.Test",
            """Anfrage: { "apiKey": "sk-streng-geheim" } an den Anbieter""");

        foreach (var target in config.AllTargets.OfType<TargetWithLayout>())
        {
            var rendered = target.Layout.Render(ev);
            Assert.DoesNotContain("sk-streng-geheim", rendered, StringComparison.Ordinal);
            // Der Rest der Meldung muss lesbar bleiben — sonst hätte die Maskierung zu viel gefressen.
            Assert.Contains("an den Anbieter", rendered, StringComparison.Ordinal);
        }
    }
}
