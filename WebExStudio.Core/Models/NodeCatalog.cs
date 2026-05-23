namespace WebExStudio.Core.Models;

/// <summary>
/// Static registry of all known node types and their editor metadata.
/// </summary>
public static class NodeCatalog
{
    private static readonly List<NodeDefinition> _all =
    [
        // ── Navigation ───────────────────────────────────────────────────────
        new()
        {
            Type = "goto", DisplayName = "Goto / Navigate", Category = "Navigation",
            Description = "Navigiert zu einer URL und wartet auf das Laden der Seite.",
            Color = "#1565C0", Icon = "🌐",
            Properties =
            [
                new() { Key = "url", Label = "URL", Kind = PropertyKind.Url, Required = true, Placeholder = "{host}/pfad" },
                new() { Key = "wait_ms", Label = "Wartezeit (ms)", Kind = PropertyKind.Number, DefaultValue = "0" },
            ]
        },
        new()
        {
            Type = "open_tab", DisplayName = "Tab öffnen", Category = "Navigation",
            Description = "Öffnet einen neuen Browser-Tab.",
            Color = "#1565C0", Icon = "➕",
            Properties =
            [
                new() { Key = "url", Label = "URL", Kind = PropertyKind.Url, Placeholder = "{host}" },
            ]
        },
        new()
        {
            Type = "close_tab", DisplayName = "Tab schließen", Category = "Navigation",
            Description = "Schließt den aktuellen Tab.",
            Color = "#1565C0", Icon = "✖",
        },
        new()
        {
            Type = "get_links", DisplayName = "Links sammeln", Category = "Navigation",
            Description = "Sammelt Links von der Seite und führt Sub-Actions für jeden Link aus.",
            Color = "#0277BD", Icon = "🔗",
            SubFlowSlots = ["body"],
            Properties =
            [
                new() { Key = "selector", Label = "Selektor", Kind = PropertyKind.Selector, Placeholder = "a.item" },
                new() { Key = "max", Label = "Max. Links", Kind = PropertyKind.Number, DefaultValue = "500" },
                new() { Key = "filter", Label = "Filter (Regex)", Kind = PropertyKind.Text },
                new() { Key = "ctx_key", Label = "Kontext-Variable", Kind = PropertyKind.Text, DefaultValue = "link" },
            ]
        },

        // ── Interaktion ──────────────────────────────────────────────────────
        new()
        {
            Type = "click", DisplayName = "Klicken", Category = "Interaktion",
            Description = "Klickt auf ein Element (mit automatischem Scroll und Retry).",
            Color = "#2E7D32", Icon = "🖱",
            Properties =
            [
                new() { Key = "selector", Label = "Selektor", Kind = PropertyKind.Selector, Required = true, Aliases = ["xpath", "name"] },
                new() { Key = "text", Label = "Link-Text (alternativ)", Kind = PropertyKind.Text },
                new() { Key = "scroll", Label = "Scroll to element", Kind = PropertyKind.Boolean, DefaultValue = "true" },
            ]
        },
        new()
        {
            Type = "send_keys", DisplayName = "Tastatureingabe", Category = "Interaktion",
            Description = "Gibt Text in ein Eingabefeld ein.",
            Color = "#2E7D32", Icon = "⌨",
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
            Description = "Bedingte Verzweigung basierend auf DOM, Seite oder Kontext.",
            Color = "#6A1B9A", Icon = "❓",
            SubFlowSlots = ["then", "else"],
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
            Description = "Wiederholt Sub-Actions für einen Zahlenbereich.",
            Color = "#E65100", Icon = "🔄",
            SubFlowSlots = ["body"],
            Properties =
            [
                new() { Key = "start", Label = "Start", Kind = PropertyKind.Number, DefaultValue = "0" },
                new() { Key = "end", Label = "Ende", Kind = PropertyKind.Number, Required = true },
                new() { Key = "step", Label = "Schritt", Kind = PropertyKind.Number, DefaultValue = "1" },
                new() { Key = "ctx_key", Label = "Kontext-Variable", Kind = PropertyKind.Text, DefaultValue = "i" },
                new() { Key = "exclusive", Label = "Exklusiv (< statt <=)", Kind = PropertyKind.Boolean, DefaultValue = "false" },
            ]
        },
        new()
        {
            Type = "foreach", DisplayName = "Foreach-Schleife", Category = "Kontrollfluss",
            Description = "Iteriert über eine Liste oder Dictionary aus dem Kontext.",
            Color = "#E65100", Icon = "🔁",
            SubFlowSlots = ["body"],
            Properties =
            [
                new() { Key = "items", Label = "Elemente (JSON oder Kontext-Variable)", Kind = PropertyKind.Text, Required = true },
                new() { Key = "ctx_key", Label = "Kontext-Variable", Kind = PropertyKind.Text, DefaultValue = "item" },
            ]
        },
        new()
        {
            Type = "call", DisplayName = "Tab aufrufen", Category = "Kontrollfluss",
            Description = "Ruft einen anderen Tab-Flow auf (wie eine Funktion).",
            Color = "#F57F17", Icon = "📞",
            Properties =
            [
                new() { Key = "targetTabId", Label = "Ziel-Tab ID", Kind = PropertyKind.Text, Required = true },
                new() { Key = "allow_quit", Label = "Quit erlaubt", Kind = PropertyKind.Boolean, DefaultValue = "false" },
            ]
        },
        new()
        {
            Type = "noop", DisplayName = "No-Op / Breakpoint", Category = "Kontrollfluss",
            Description = "Tut nichts — nützlich als Debug-Breakpoint.",
            Color = "#757575", Icon = "⏸",
        },
        new()
        {
            Type = "quit", DisplayName = "Browser beenden", Category = "Kontrollfluss",
            Description = "Schließt den Browser und beendet die Ausführung.",
            Color = "#B71C1C", Icon = "🚪",
            OutputPorts = 0,
            Properties =
            [
                new() { Key = "force", Label = "Erzwingen", Kind = PropertyKind.Boolean, DefaultValue = "false" },
            ]
        },

        // ── Daten ────────────────────────────────────────────────────────────
        new()
        {
            Type = "get_value", DisplayName = "Wert lesen", Category = "Daten",
            Description = "Liest einen Wert aus dem DOM (Text, Attribut, HTML) in den Kontext.",
            Color = "#00695C", Icon = "📖",
            Properties =
            [
                new() { Key = "selector", Label = "Selektor", Kind = PropertyKind.Selector, Required = true },
                new() { Key = "attr", Label = "Attribut (leer = Text)", Kind = PropertyKind.Text },
                new() { Key = "ctx_key", Label = "Kontext-Variable", Kind = PropertyKind.Text, Required = true },
                new() { Key = "regex", Label = "Regex-Extraktion", Kind = PropertyKind.Text },
                new() { Key = "filter", Label = "Filter", Kind = PropertyKind.Text },
            ]
        },
        new()
        {
            Type = "set_ctx", DisplayName = "Kontext setzen", Category = "Daten",
            Description = "Setzt oder berechnet Kontext-Variablen.",
            Color = "#00695C", Icon = "📝",
            Properties =
            [
                new() { Key = "key", Label = "Variable", Kind = PropertyKind.Text, Required = true },
                new() { Key = "value", Label = "Wert", Kind = PropertyKind.Text, Required = true },
            ]
        },
        new()
        {
            Type = "set_payload", DisplayName = "Payload setzen", Category = "Daten",
            Description = "Setzt einen Schlüssel im Payload-Objekt, das durch Verbindungen fließt.",
            Color = "#00695C", Icon = "📦",
            Properties =
            [
                new() { Key = "key", Label = "Payload-Schlüssel", Kind = PropertyKind.Text, Required = true },
                new() { Key = "value", Label = "Wert", Kind = PropertyKind.Text, Required = true },
            ]
        },
        new()
        {
            Type = "read_file", DisplayName = "Datei lesen", Category = "Daten",
            Description = "Liest eine Datei in den Kontext.",
            Color = "#004D40", Icon = "📄",
            Properties =
            [
                new() { Key = "path", Label = "Pfad", Kind = PropertyKind.FilePath, Required = true },
                new() { Key = "ctx_key", Label = "Kontext-Variable", Kind = PropertyKind.Text, DefaultValue = "file_content" },
                new() { Key = "mode", Label = "Modus", Kind = PropertyKind.Dropdown, DefaultValue = "full" },
            ]
        },
        new()
        {
            Type = "write_file", DisplayName = "Datei schreiben", Category = "Daten",
            Description = "Schreibt einen Wert in eine Datei.",
            Color = "#004D40", Icon = "💾",
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
            Description = "Erkennt CAPTCHA und wartet auf manuelle Lösung.",
            Color = "#4527A0", Icon = "🤖",
            Properties =
            [
                new() { Key = "timeout_s", Label = "Timeout (Sek.)", Kind = PropertyKind.Number, DefaultValue = "120" },
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
