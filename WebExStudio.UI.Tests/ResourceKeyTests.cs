using System.Text.RegularExpressions;
using Xunit;

namespace WebExStudio.UI.Tests;

/// <summary>
/// Schützt den Kroste-Look vor STILLEN Fehlern. Weder ein toter
/// <c>{DynamicResource XyzBrush}</c> noch eine tote <c>Classes="accent"</c> erzeugt einen
/// Compile-Fehler — beides rendert einfach falsch und fällt oft erst Releases später auf.
/// Dieser Test macht daraus einen roten Testlauf.
///
/// Geprüft wird dreierlei:
/// <list type="number">
/// <item>Jeder referenzierte Resource-Key ist in App.axaml definiert.</item>
/// <item>Kein <c>#RRGGBB</c>-Literal außerhalb der Palette (sonst lässt sich der Look nicht
///   mehr zentral ändern und driftet).</item>
/// <item>Keine doppelten <c>x:Key</c> (die werfen erst beim Laden des Fensters).</item>
/// </list>
/// </summary>
public class ResourceKeyTests
{
    /// <summary>Repo-Wurzel über die .slnx finden (vom Testausgabe-Verzeichnis aus hochlaufen).</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "WebExStudio.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Repo-Wurzel (WebExStudio.slnx) nicht gefunden.");
    }

    private static string UiDir => Path.Combine(RepoRoot(), "WebExStudio.UI");

    private static string[] AxamlFiles() =>
        Directory.GetFiles(UiDir, "*.axaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

    private static string AppAxaml => File.ReadAllText(Path.Combine(UiDir, "App.axaml"));

    [Fact]
    public void SanityCheck_FindetDieXamlDateien()
    {
        // Ein Abgleich, der nichts findet, weil die Pfadauflösung danebengreift, ist schlimmer
        // als gar keiner — deshalb zuerst prüfen, dass überhaupt Dateien eingesammelt werden.
        Assert.True(AxamlFiles().Length >= 10, "Es wurden kaum .axaml-Dateien gefunden — Pfadauflösung prüfen.");
        Assert.Contains("KrosteGoldBrush", AppAxaml);
    }

    [Fact]
    public void JederReferenzierteResourceKey_IstDefiniert()
    {
        var defined = Regex.Matches(AppAxaml, "x:Key=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var missing = new List<string>();
        foreach (var file in AxamlFiles())
        {
            var text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, @"\{(?:Dynamic|Static)Resource\s+([A-Za-z0-9_.]+)\s*\}"))
            {
                var key = m.Groups[1].Value;
                // Framework-Keys (FluentTheme) sind am System-Präfix erkennbar und stehen nicht bei uns.
                if (key.StartsWith("System", StringComparison.Ordinal)) continue;
                if (!defined.Contains(key))
                    missing.Add($"{Path.GetFileName(file)}: {key}");
            }
        }

        Assert.True(missing.Count == 0,
            "Diese Resource-Keys werden referenziert, sind aber in App.axaml nicht definiert "
            + "(rendert still falsch):\n  " + string.Join("\n  ", missing));
    }

    [Fact]
    public void KeineFarbliterale_AusserhalbDerPalette()
    {
        var offenders = new List<string>();
        foreach (var file in AxamlFiles())
        {
            if (Path.GetFileName(file) == "App.axaml") continue;   // die Palette selbst
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (Regex.IsMatch(lines[i], "=\"#[0-9A-Fa-f]{6,8}\""))
                    offenders.Add($"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
            }
        }

        Assert.True(offenders.Count == 0,
            "Farben gehören als Rolle in die Palette (App.axaml), nicht als Literal ins Fenster-XAML:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void KeineDoppeltenResourceKeys()
    {
        var duplicates = Regex.Matches(AppAxaml, "x:Key=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .GroupBy(k => k, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} ({g.Count()}x)")
            .ToList();

        Assert.True(duplicates.Count == 0,
            "Doppelte x:Key in App.axaml (wirft erst beim Laden):\n  " + string.Join("\n  ", duplicates));
    }

    [Fact]
    public void AlleFensterErbenVonChromeWindow()
    {
        // Kroste-Standard: kein OS-Chrome. Ein neu angelegtes Fenster, das direkt von Window erbt,
        // fällt optisch aus der App heraus, ohne dass irgendetwas fehlschlägt.
        var offenders = new List<string>();
        foreach (var file in AxamlFiles())
        {
            var text = File.ReadAllText(file);
            if (text.TrimStart().StartsWith("<Window", StringComparison.Ordinal))
                offenders.Add(Path.GetFileName(file));
        }

        Assert.True(offenders.Count == 0,
            "Diese Fenster erben direkt von Window statt von ChromeWindow:\n  " + string.Join("\n  ", offenders));
    }
}
