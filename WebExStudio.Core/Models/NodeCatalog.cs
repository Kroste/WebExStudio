namespace WebExStudio.Core.Models;

/// <summary>
/// Static registry of all known node types and their editor metadata.
/// </summary>
public static class NodeCatalog
{
    private static readonly List<NodeDefinition> _all =
    [
        // ── Start ────────────────────────────────────────────────────────────
        new()
        {
            Type = "function", DisplayName = "Function / Start", Category = "Start",
            Description = "Startpunkt des Flows. Setzt die initialen Payload-Werte (ersetzt Targets).",
            Color = "#D84315", Icon = "🚀",
            Example = "payload = {\"host\":\"https://example.com\",\"user\":\"max\"}  →  im Flow nutzbar als {payload.host}",
            Properties =
            [
                new() { Key = "payload", Label = "Start-Payload (JSON)", Kind = PropertyKind.Code, DefaultValue = "{\n  \"host\": \"https://example.com\"\n}" },
            ]
        },

        // ── Navigation ───────────────────────────────────────────────────────
        new()
        {
            Type = "goto", DisplayName = "Goto / Navigate", Category = "Navigation",
            Description = "Navigiert zu einer URL und wartet auf das Laden der Seite.",
            Color = "#1565C0", Icon = "🌐",
            Example = "url = {payload.host}/login  →  öffnet die Login-Seite und wartet aufs Laden.",
            Properties =
            [
                new() { Key = "url", Label = "URL", Kind = PropertyKind.Url, Required = true, Placeholder = "{payload.host}/pfad" },
                new() { Key = "wait_ms", Label = "Wartezeit (ms)", Kind = PropertyKind.Number, DefaultValue = "0" },
            ]
        },
        new()
        {
            Type = "open_tab", DisplayName = "Tab öffnen", Category = "Navigation",
            Description = "Öffnet einen neuen Browser-Tab und wechselt dorthin.",
            Color = "#1565C0", Icon = "➕",
            Example = "url = https://example.com  →  öffnet einen neuen Tab; nachfolgende Aktionen laufen darin.",
            Properties =
            [
                new() { Key = "url", Label = "URL", Kind = PropertyKind.Url, Placeholder = "{payload.host}" },
            ]
        },
        new()
        {
            Type = "close_tab", DisplayName = "Tab schließen", Category = "Navigation",
            Description = "Schließt den aktuellen Tab.",
            Color = "#1565C0", Icon = "✖",
            Example = "Schließt den aktuellen Tab und wechselt zum verbleibenden Tab zurück.",
        },
        new()
        {
            Type = "get_links", DisplayName = "Links sammeln", Category = "Navigation",
            Description = "Sammelt Links und führt den 'je Link'-Ausgang für jeden Link aus.",
            Color = "#0277BD", Icon = "🔗",
            OutputPorts = 2, OutputLabels = ["je Link", "fertig"],
            Example = "selector = a.product  →  'je Link'-Ausgang läuft je Link; aktueller Link in {link}.",
            Properties =
            [
                new() { Key = "selector", Label = "Selektor", Kind = PropertyKind.Selector, Placeholder = "a.item" },
                new() { Key = "max", Label = "Max. Links", Kind = PropertyKind.Number, DefaultValue = "500" },
                new() { Key = "filter", Label = "Filter (Regex)", Kind = PropertyKind.Text },
                new() { Key = "ctx_key", Label = "Payload-Schlüssel", Kind = PropertyKind.Text, DefaultValue = "link" },
            ]
        },

        // ── Interaktion ──────────────────────────────────────────────────────
        new()
        {
            Type = "click", DisplayName = "Klicken", Category = "Interaktion",
            Description = "Klickt auf ein Element (mit automatischem Scroll und Retry). "
                + "Für Download-Buttons 'expect_download' = true setzen — dann wird auf den Download "
                + "gewartet und die Datei mit echtem Namen im Downloadordner gespeichert.",
            Color = "#2E7D32", Icon = "🖱",
            Example = "selector = button#submit  →  klickt den Senden-Button. Download: selector = a.download, expect_download = true.",
            Properties =
            [
                new() { Key = "selector", Label = "Selektor", Kind = PropertyKind.Selector, Required = true, Aliases = ["xpath", "name"] },
                new() { Key = "text", Label = "Link-Text (alternativ)", Kind = PropertyKind.Text },
                new() { Key = "scroll", Label = "Scroll to element", Kind = PropertyKind.Boolean, DefaultValue = "true" },
                new() { Key = "expect_download", Label = "Auf Download warten & speichern", Kind = PropertyKind.Boolean, DefaultValue = "false" },
                new() { Key = "download_timeout_ms", Label = "Download-Timeout (ms)", Kind = PropertyKind.Number, DefaultValue = "60000" },
            ]
        },
        new()
        {
            Type = "send_keys", DisplayName = "Tastatureingabe", Category = "Interaktion",
            Description = "Gibt Text in ein Eingabefeld ein.",
            Color = "#2E7D32", Icon = "⌨",
            Example = "selector = input[name=q], value = {payload.suchwort}  →  tippt den Suchbegriff ein.",
            Properties =
            [
                new() { Key = "selector", Label = "Selektor", Kind = PropertyKind.Selector, Required = true, Aliases = ["name", "xpath"] },
                new() { Key = "value", Label = "Wert", Kind = PropertyKind.Text, Required = true },
                new() { Key = "clear", Label = "Feld leeren vorher", Kind = PropertyKind.Boolean, DefaultValue = "true" },
                new() { Key = "append", Label = "Anhängen", Kind = PropertyKind.Boolean, DefaultValue = "false" },
            ]
        },
        new()
        {
            Type = "wait_for", DisplayName = "Warten auf Element", Category = "Interaktion",
            Description = "Wartet bis ein Element sichtbar/vorhanden ist.",
            Color = "#2E7D32", Icon = "⏳",
            Example = "selector = .ergebnis, state = visible  →  wartet bis Ergebnisse sichtbar sind.",
            Properties =
            [
                new() { Key = "selector", Label = "Selektor", Kind = PropertyKind.Selector, Required = true },
                new() { Key = "timeout_ms", Label = "Timeout (ms)", Kind = PropertyKind.Number, DefaultValue = "30000" },
                new() { Key = "state", Label = "Zustand", Kind = PropertyKind.Dropdown, DefaultValue = "visible" },
            ]
        },
        new()
        {
            Type = "sleep", DisplayName = "Pause", Category = "Interaktion",
            Description = "Pausiert die Ausführung für eine bestimmte Zeit.",
            Color = "#2E7D32", Icon = "💤",
            Example = "seconds = 2  →  pausiert 2 Sekunden.",
            Properties =
            [
                new() { Key = "seconds", Label = "Sekunden", Kind = PropertyKind.Number, DefaultValue = "1" },
            ]
        },
        new()
        {
            Type = "menu_path", DisplayName = "Menü-Navigation", Category = "Interaktion",
            Description = "Navigiert ein hierarchisches Menü (Hover → Klick).",
            Color = "#388E3C", Icon = "📋",
            Example = "path = Datei, Export, PDF  →  hovert/klickt durch das Menü zum PDF-Export.",
            Properties =
            [
                new() { Key = "path", Label = "Menü-Pfad (kommagetrennt)", Kind = PropertyKind.Text, Required = true, Aliases = ["items"] },
                new() { Key = "selector_prefix", Label = "Selektor-Präfix", Kind = PropertyKind.Text },
            ]
        },

        // ── Kontrollfluss ────────────────────────────────────────────────────
        new()
        {
            Type = "if_then_else", DisplayName = "If / Then / Else", Category = "Kontrollfluss",
            Description = "Bedingte Verzweigung. Verbinde die Ausgänge 'then' und 'else' mit den Folge-Nodes. "
                + "Gültige Bedingungen (condition): element_exists, element_visible, element_text, page_url, "
                + "page_title, page_contains, page_matches, payload_equals, payload_contains. "
                + "Vergleichswert in 'value'; DOM-Selektor (bzw. bei payload_* der Payload-Schlüssel) in 'selector'.",
            Color = "#6A1B9A", Icon = "❓",
            OutputPorts = 2, OutputLabels = ["then", "else"],
            Example = "condition = element_exists, selector = .fehler  →  Treffer: 'then'-Ausgang, sonst: 'else'-Ausgang.",
            Properties =
            [
                new() { Key = "condition", Label = "Bedingung-Typ", Kind = PropertyKind.Dropdown, DefaultValue = "element_exists" },
                new() { Key = "selector", Label = "Selektor (für DOM-Bedingungen)", Kind = PropertyKind.Selector },
                new() { Key = "value", Label = "Vergleichswert", Kind = PropertyKind.Text },
                new() { Key = "regex", Label = "Regex", Kind = PropertyKind.Boolean, DefaultValue = "false" },
                new() { Key = "negate", Label = "Negieren", Kind = PropertyKind.Boolean, DefaultValue = "false" },
            ]
        },
        new()
        {
            Type = "for_range", DisplayName = "For-Schleife", Category = "Kontrollfluss",
            Description = "Wiederholt den 'Schleife'-Ausgang für einen Zahlenbereich.",
            Color = "#E65100", Icon = "🔄",
            OutputPorts = 2, OutputLabels = ["Schleife", "fertig"],
            Example = "start = 1, end = 5  →  'Schleife'-Ausgang läuft 5x; aktueller Wert in {i}.",
            Properties =
            [
                new() { Key = "start", Label = "Start", Kind = PropertyKind.Number, DefaultValue = "0" },
                new() { Key = "end", Label = "Ende", Kind = PropertyKind.Number, Required = true },
                new() { Key = "step", Label = "Schritt", Kind = PropertyKind.Number, DefaultValue = "1" },
                new() { Key = "ctx_key", Label = "Payload-Schlüssel", Kind = PropertyKind.Text, DefaultValue = "i" },
                new() { Key = "exclusive", Label = "Exklusiv (< statt <=)", Kind = PropertyKind.Boolean, DefaultValue = "false" },
            ]
        },
        new()
        {
            Type = "foreach", DisplayName = "Foreach-Schleife", Category = "Kontrollfluss",
            Description = "Iteriert über eine Liste/Dictionary. Verbinde den 'Element'-Ausgang mit dem Schleifenkörper.",
            Color = "#E65100", Icon = "🔁",
            OutputPorts = 2, OutputLabels = ["Element", "fertig"],
            Example = "items = {payload.targets}  →  'Element'-Ausgang läuft je Element; Felder als {payload.host} usw.",
            Properties =
            [
                new() { Key = "items", Label = "Elemente (JSON oder {payload.key})", Kind = PropertyKind.Text, Required = true },
                new() { Key = "ctx_key", Label = "Payload-Schlüssel", Kind = PropertyKind.Text, DefaultValue = "item" },
            ]
        },
        new()
        {
            Type = "call", DisplayName = "Subnode aufrufen", Category = "Kontrollfluss",
            Description = "Ruft einen benannten Subnode als Unterprogramm auf.",
            Color = "#F57F17", Icon = "📞",
            Example = "target = configuration.general.datetime.daylightSavings  →  führt diesen Subnode aus.",
            Properties =
            [
                new() { Key = "target", Label = "Ziel-Subnode", Kind = PropertyKind.Dropdown, Required = true },
                new() { Key = "allow_quit", Label = "Quit erlaubt", Kind = PropertyKind.Boolean, DefaultValue = "false" },
            ]
        },
        new()
        {
            Type = "noop", DisplayName = "No-Op / Breakpoint", Category = "Kontrollfluss",
            Description = "Tut nichts — nützlich als Platzhalter oder Debug-Breakpoint.",
            Color = "#757575", Icon = "⏸",
            Example = "Ohne Konfiguration — überspringt einfach und läuft weiter.",
        },
        new()
        {
            Type = "quit", DisplayName = "Beenden", Category = "Kontrollfluss",
            Description = "Beendet die Ausführung dieses Flows sofort.",
            Color = "#B71C1C", Icon = "🚪",
            OutputPorts = 0,
            Example = "Stoppt den Flow an dieser Stelle (z. B. nach einem Fehlerzweig).",
            Properties =
            [
                new() { Key = "force", Label = "Erzwingen", Kind = PropertyKind.Boolean, DefaultValue = "false" },
            ]
        },

        // ── Daten ────────────────────────────────────────────────────────────
        new()
        {
            Type = "get_value", DisplayName = "Wert lesen", Category = "Daten",
            Description = "Liest einen Wert aus dem DOM (Text, Attribut) ins Payload.",
            Color = "#00695C", Icon = "📖",
            Example = "selector = .preis, ctx_key = preis  →  liest den Preis-Text nach {preis}.",
            Properties =
            [
                new() { Key = "selector", Label = "Selektor", Kind = PropertyKind.Selector, Required = true },
                new() { Key = "attr", Label = "Attribut (leer = Text)", Kind = PropertyKind.Text },
                new() { Key = "ctx_key", Label = "Payload-Schlüssel", Kind = PropertyKind.Text, Required = true },
                new() { Key = "regex", Label = "Regex-Extraktion", Kind = PropertyKind.Text },
                new() { Key = "filter", Label = "Filter", Kind = PropertyKind.Text },
            ]
        },
        new()
        {
            Type = "set_payload", DisplayName = "Payload setzen", Category = "Daten",
            Description = "Setzt einen Schlüssel im Payload-Objekt, das durch Verbindungen fließt.",
            Color = "#00695C", Icon = "📦",
            Example = "key = status, value = ok  →  schreibt status ins Payload ({payload.status}).",
            Properties =
            [
                new() { Key = "key", Label = "Payload-Schlüssel", Kind = PropertyKind.Text, Required = true },
                new() { Key = "value", Label = "Wert", Kind = PropertyKind.Text, Required = true },
            ]
        },
        new()
        {
            Type = "debug", DisplayName = "Debug-Ausgabe", Category = "Daten",
            Description = "Gibt Payload/Kontext im Ausführungsprotokoll aus. Kann den Flow anhalten.",
            Color = "#00838F", Icon = "🐞",
            Example = "source = payload, pause = true  →  zeigt den Payload im Protokoll und wartet auf „Weiter“.",
            Properties =
            [
                new() { Key = "source", Label = "Quelle (payload/ctx/both)", Kind = PropertyKind.Dropdown, DefaultValue = "payload" },
                new() { Key = "key", Label = "Nur Schlüssel (optional)", Kind = PropertyKind.Text },
                new() { Key = "label", Label = "Label (optional)", Kind = PropertyKind.Text },
                new() { Key = "pause", Label = "Anhalten (zum Inspizieren)", Kind = PropertyKind.Boolean, DefaultValue = "false" },
            ]
        },
        new()
        {
            Type = "read_file", DisplayName = "Datei lesen", Category = "Daten",
            Description = "Liest eine Datei ins Payload.",
            Color = "#004D40", Icon = "📄",
            Example = "path = daten.txt, ctx_key = inhalt  →  liest die Datei nach {inhalt}.",
            Properties =
            [
                new() { Key = "path", Label = "Pfad", Kind = PropertyKind.FilePath, Required = true },
                new() { Key = "ctx_key", Label = "Payload-Schlüssel", Kind = PropertyKind.Text, DefaultValue = "file_content" },
                new() { Key = "mode", Label = "Modus", Kind = PropertyKind.Dropdown, DefaultValue = "full" },
            ]
        },
        new()
        {
            Type = "write_file", DisplayName = "Datei schreiben", Category = "Daten",
            Description = "Schreibt einen Wert in eine Datei.",
            Color = "#004D40", Icon = "💾",
            Example = "path = out.txt, value = {payload.ergebnis}  →  schreibt das Ergebnis in die Datei.",
            Properties =
            [
                new() { Key = "path", Label = "Pfad", Kind = PropertyKind.FilePath, Required = true },
                new() { Key = "value", Label = "Inhalt", Kind = PropertyKind.MultilineText, Required = true },
                new() { Key = "append", Label = "Anhängen", Kind = PropertyKind.Boolean, DefaultValue = "false" },
            ]
        },

        // ── Erweitert ────────────────────────────────────────────────────────
        new()
        {
            Type = "download_url", DisplayName = "URL herunterladen", Category = "Erweitert",
            Description = "Lädt eine Datei von einer URL herunter.",
            Color = "#4527A0", Icon = "⬇",
            Example = "url = {payload.host}/datei.pdf  →  lädt die Datei in den Download-Ordner.",
            Properties =
            [
                new() { Key = "url", Label = "URL", Kind = PropertyKind.Url, Required = true },
                new() { Key = "filename", Label = "Dateiname", Kind = PropertyKind.Text },
                new() { Key = "timeout_ms", Label = "Timeout (ms)", Kind = PropertyKind.Number, DefaultValue = "60000" },
            ]
        },
        new()
        {
            Type = "captcha_guard", DisplayName = "CAPTCHA-Schutz", Category = "Erweitert",
            Description = "Erkennt CAPTCHA, klickt (optional) die erste „Ich bin kein Roboter\"-Checkbox "
                + "und wartet dann auf die (ggf. manuelle) Lösung.",
            Color = "#4527A0", Icon = "🤖",
            Example = "auto_click = true, timeout_s = 120  →  klickt die Checkbox; ein folgendes Bild-Rätsel löst der Nutzer.",
            Properties =
            [
                new() { Key = "timeout_s", Label = "Timeout (Sek.)", Kind = PropertyKind.Number, DefaultValue = "120" },
                new() { Key = "auto_click", Label = "Erste Checkbox automatisch klicken", Kind = PropertyKind.Boolean, DefaultValue = "true" },
            ]
        },

        // ── Anmerkung (reine Anzeige, keine Funktion) ─────────────────────────
        new()
        {
            Type = "caption", DisplayName = "Caption / Überschrift", Category = "Anmerkung",
            Description = "Zeigt eine große Überschrift auf der Arbeitsfläche an. Keine Funktion.",
            Color = "#37474F", Icon = "🏷",
            InputPorts = 0, OutputPorts = 0,
            Example = "text = Konfiguration  →  große Überschrift zur Gliederung des Flows.",
            Properties =
            [
                new() { Key = "text", Label = "Text", Kind = PropertyKind.Text, DefaultValue = "Überschrift" },
            ]
        },
        new()
        {
            Type = "label", DisplayName = "Label / Kommentar", Category = "Anmerkung",
            Description = "Zeigt einen Kommentartext auf der Arbeitsfläche an. Keine Funktion.",
            Color = "#37474F", Icon = "💬",
            InputPorts = 0, OutputPorts = 0,
            Example = "text = Hier wird eingeloggt  →  kleiner Kommentar zur Erläuterung.",
            Properties =
            [
                new() { Key = "text", Label = "Text", Kind = PropertyKind.MultilineText, DefaultValue = "Kommentar" },
            ]
        },
    ];

    private static readonly Dictionary<string, NodeDefinition> _byType =
        _all.ToDictionary(d => d.Type, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<NodeDefinition> All => _all;

    public static IEnumerable<string> Categories =>
        _all.Select(d => d.Category).Distinct();

    public static IEnumerable<NodeDefinition> GetByCategory(string category) =>
        _all.Where(d => d.Category == category);

    public static NodeDefinition? Get(string type) =>
        _byType.GetValueOrDefault(type);

    public static NodeDefinition GetOrUnknown(string type) =>
        _byType.TryGetValue(type, out var def) ? def : new NodeDefinition
        {
            Type = type, DisplayName = type, Category = "Unbekannt",
            Color = "#546E7A", Icon = "❔"
        };
}
