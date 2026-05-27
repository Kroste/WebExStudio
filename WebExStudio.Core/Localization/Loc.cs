using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace WebExStudio.Core.Localization;

/// <summary>
/// Reaktiver Sprachdienst (Laufzeit-Umschaltung). Die Übersetzungen liegen als eingebettete
/// JSON-Wörterbücher (<c>Localization/&lt;code&gt;.json</c>, z. B. de.json, en.json). Eine neue
/// Sprache = einfach eine weitere JSON-Datei hinzufügen.
///
/// Verwendung:
/// <list type="bullet">
/// <item>XAML: <c>{l:Tr Schlüssel}</c> (siehe TrExtension in der UI) — aktualisiert sich live.</item>
/// <item>Code: <c>Loc.T("Schlüssel")</c>.</item>
/// </list>
/// Unbekannte Schlüssel liefern den Schlüssel selbst zurück (so fällt Fehlendes sofort auf).
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private readonly Dictionary<string, Dictionary<string, string>> _langs;
    private Dictionary<string, string> _current;

    /// <summary>Aktueller Sprachcode (z. B. "de", "en").</summary>
    public string Language { get; private set; }

    private Loc()
    {
        _langs = LoadEmbedded();
        Language = _langs.ContainsKey("de") ? "de" : _langs.Keys.FirstOrDefault() ?? "de";
        _current = _langs.GetValueOrDefault(Language) ?? new();
    }

    /// <summary>Übersetzung für den Schlüssel; fehlt sie, kommt der Schlüssel selbst zurück.</summary>
    public string this[string key] => _current.TryGetValue(key, out var v) ? v : key;

    /// <summary>Verfügbare Sprachcodes (alphabetisch).</summary>
    public IReadOnlyList<string> Languages => _langs.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

    /// <summary>Prüft, ob eine Sprache einen Schlüssel kennt (für Vollständigkeits-Tests).</summary>
    public bool Has(string lang, string key) =>
        _langs.TryGetValue(lang, out var d) && d.ContainsKey(key);

    /// <summary>Alle Schlüssel einer Sprache (für Vollständigkeits-Tests).</summary>
    public IReadOnlyCollection<string> Keys(string lang) =>
        _langs.TryGetValue(lang, out var d) ? d.Keys.ToList() : [];

    /// <summary>Eigenname einer Sprache (Eintrag <c>@name</c> im Wörterbuch), sonst der Code.</summary>
    public string NameOf(string lang) =>
        _langs.TryGetValue(lang, out var d) && d.TryGetValue("@name", out var n) ? n : lang;

    /// <summary>Flaggen-Emoji einer Sprache (Eintrag <c>@flag</c> im Wörterbuch), sonst leer.</summary>
    public string FlagOf(string lang) =>
        _langs.TryGetValue(lang, out var d) && d.TryGetValue("@flag", out var f) ? f : string.Empty;

    /// <summary>Schaltet die Sprache um und frischt alle gebundenen Texte auf.</summary>
    public void SetLanguage(string lang)
    {
        if (string.IsNullOrEmpty(lang) || lang == Language || !_langs.TryGetValue(lang, out var d)) return;
        _current = d;
        Language = lang;
        // Leerer Name = „alle Properties geändert" + Indexer-Notification → alle {l:Tr}-Bindings neu auswerten.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
    }

    /// <summary>Übersetzung im Code (nicht live; für transiente Texte wie Statusmeldungen).</summary>
    public static string T(string key) => Instance[key];

    /// <summary>
    /// Übersetzung mit Rückfallwert: Liefert den Eintrag der aktuellen Sprache, sonst <paramref name="fallback"/>.
    /// Gedacht für den Node-Katalog — die deutschen Literale bleiben der Standard (Fallback), Übersetzungen
    /// (z. B. en.json) werden nur dort hinterlegt, wo nötig.
    /// </summary>
    public string Tr(string key, string fallback) =>
        _current.TryGetValue(key, out var v) ? v : fallback;

    /// <summary>Statische Kurzform von <see cref="Tr(string,string)"/>.</summary>
    public static string T(string key, string fallback) => Instance.Tr(key, fallback);

    public event PropertyChangedEventHandler? PropertyChanged;

    private static Dictionary<string, Dictionary<string, string>> LoadEmbedded()
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var asm = typeof(Loc).Assembly;
        foreach (var res in asm.GetManifestResourceNames())
        {
            if (!res.Contains(".Localization.", StringComparison.Ordinal) || !res.EndsWith(".json", StringComparison.Ordinal))
                continue;
            var stem = res[..^".json".Length];                 // …Localization.de
            var lang = stem[(stem.LastIndexOf('.') + 1)..];     // de
            using var stream = asm.GetManifestResourceStream(res);
            if (stream is null) continue;
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
            if (dict is not null) result[lang] = new Dictionary<string, string>(dict, StringComparer.Ordinal);
        }
        return result;
    }
}
