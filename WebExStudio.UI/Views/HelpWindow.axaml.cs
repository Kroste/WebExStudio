using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NLog;

namespace WebExStudio.UI.Views;

public partial class HelpWindow : ChromeWindow
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public HelpWindow() : this(null) { }

    public HelpWindow(System.Action<string>? onLoadExample)
    {
        InitializeComponent();
        // Beispiel-JSON aus der Hilfe in den Flow übernehmen und das Hilfefenster schließen.
        System.Action<string>? load = onLoadExample is null
            ? null
            : json => { onLoadExample(json); Close(); };
        HelpContent.Content = MarkdownRenderer.Build(LoadReadme(), load);
    }

    /// <summary>
    /// Liest die eingebettete README (Quelle der Hilfe → bleibt automatisch synchron). Wählt die
    /// Sprachvariante passend zur UI-Sprache (z. B. <c>README.en.md</c>); fällt auf die deutsche
    /// <c>README.md</c> zurück, wenn es keine Übersetzung gibt.
    /// </summary>
    private static string LoadReadme()
    {
        var asm = typeof(HelpWindow).Assembly;
        var lang = Core.Localization.Loc.Instance.Language;

        // README.md ist Englisch (Basis/Default). Reihenfolge: sprachspezifische Variante (de/fr/ru)
        // → README.md (Englisch) als Universal-Fallback. Für Englisch gibt es keine eigene Datei,
        // dort greift direkt README.md.
        var candidates = new List<string>();
        if (!string.Equals(lang, "en", System.StringComparison.OrdinalIgnoreCase))
            candidates.Add($"WebExStudio.UI.README.{lang}.md");
        candidates.Add("WebExStudio.UI.README.md");

        foreach (var name in candidates)
        {
            try
            {
                using var stream = asm.GetManifestResourceStream(name);
                if (stream is null) continue;
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (System.Exception ex)
            {
                Log.Warn("Hilfe: README '{0}' konnte nicht geladen werden: {1}", name, ex.Message);
            }
        }

        Log.Warn("Hilfe: keine eingebettete README gefunden (Sprache {0}).", lang);
        return "# Hilfe\n\nDie eingebettete README konnte nicht geladen werden. "
             + "Die vollständige Dokumentation liegt als `README.md` im Projektverzeichnis.";
    }


}
