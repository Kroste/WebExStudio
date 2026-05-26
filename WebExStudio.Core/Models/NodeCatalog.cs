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
            Type = "function", DisplayName = "Input", Category = "Start",
            Description = "Input-/Startpunkt des Flows. Setzt die initialen Payload-Werte (ersetzt Targets).",
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
            Type = "goto", DisplayName = "Navigate", Category = "Navigation",
            Description = "Navigiert zu einer URL und wartet auf das Laden der Seite. Mit 'new_tab' = true "
                + "wird stattdessen ein neuer Tab geöffnet und dorthin gewechselt (ersetzt den open_tab-Node).",
            Color = "#1565C0", Icon = "🌐",
            Example = "url = {payload.host}/login  →  öffnet die Login-Seite. new_tab = true → in neuem Tab.",
            Properties =
            [
                new() { Key = "url", Label = "URL", Kind = PropertyKind.Url, Required = true, Placeholder = "{payload.host}/pfad" },
                new() { Key = "new_tab", Label = "In neuem Tab öffnen", Kind = PropertyKind.Boolean, DefaultValue = "false" },
                new() { Key = "wait_ms", Label = "Wartezeit (ms)", Kind = PropertyKind.Number, DefaultValue = "0" },
            ]
        },
        // Ersetzt durch 'goto' mit new_tab=true — als versteckter Alias erhalten (alte Flows).
        new()
        {
            Type = "open_tab", DisplayName = "Tab öffnen", Category = "Navigation", Hidden = true,
            Description = "Öffnet einen neuen Browser-Tab und wechselt dorthin. (Ersetzt durch Navigate + new_tab.)",
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
            Type = "send_keys", DisplayName = "Text eingeben", Category = "Interaktion",
            Description = "Gibt Text in ein Eingabefeld ein (füllt das Feld). Für einzelne (Sonder-)Tasten "
                + "wie Enter/Tab/Escape den 'Taste drücken'-Node (press_key) verwenden.",
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
        new()
        {
            Type = "scroll", DisplayName = "Scrollen", Category = "Interaktion",
            Description = "Scrollt die Seite nach oben/unten oder zu einem Element. Mehrfaches Scrollen "
                + "nach unten lädt „lazy\" nachgeladene Inhalte (z. B. Forenlisten) nach.",
            Color = "#2E7D32", Icon = "↕",
            Example = "to = bottom, times = 3  →  scrollt dreimal ans Seitenende (lädt mehr Treffer).",
            Properties =
            [
                new() { Key = "to", Label = "Ziel (top/bottom)", Kind = PropertyKind.Dropdown, DefaultValue = "bottom" },
                new() { Key = "selector", Label = "Zu Element (Selektor, optional)", Kind = PropertyKind.Selector },
                new() { Key = "times", Label = "Wiederholungen", Kind = PropertyKind.Number, DefaultValue = "1" },
                new() { Key = "delay_ms", Label = "Pause zwischen (ms)", Kind = PropertyKind.Number, DefaultValue = "500" },
            ]
        },
        new()
        {
            Type = "press_key", DisplayName = "Taste drücken", Category = "Interaktion",
            Description = "Drückt eine (Sonder-)Taste oder Tastenkombination — z. B. Enter zum Absenden, "
                + "Escape, Tab oder Control+A. Mit Selektor auf einem Element, sonst global.",
            Color = "#2E7D32", Icon = "⏎",
            Example = "key = Enter, selector = input[name=q]  →  sendet das Suchfeld ab.",
            Properties =
            [
                new() { Key = "key", Label = "Taste (z. B. Enter, Escape, Control+A)", Kind = PropertyKind.Text, Required = true },
                new() { Key = "selector", Label = "Selektor (optional)", Kind = PropertyKind.Selector },
            ]
        },
        new()
        {
            Type = "select_option", DisplayName = "Dropdown wählen", Category = "Interaktion",
            Description = "Wählt einen Eintrag in einem <select>-Dropdown — nach Wert, sichtbarem Text (Label) oder Index.",
            Color = "#2E7D32", Icon = "▼",
            Example = "selector = select#land, by = label, value = Deutschland  →  wählt „Deutschland\".",
            Properties =
            [
                new() { Key = "selector", Label = "Selektor", Kind = PropertyKind.Selector, Required = true },
                new() { Key = "by", Label = "Auswahl nach (value/label/index)", Kind = PropertyKind.Dropdown, DefaultValue = "value" },
                new() { Key = "value", Label = "Wert / Text / Index", Kind = PropertyKind.Text, Required = true },
            ]
        },
        new()
        {
            Type = "hover", DisplayName = "Überfahren (Hover)", Category = "Interaktion",
            Description = "Fährt mit der Maus über ein Element (z. B. um ein Menü/Tooltip einzublenden).",
            Color = "#2E7D32", Icon = "👆",
            Example = "selector = .menue-eintrag  →  klappt das Untermenü auf.",
            Properties =
            [
                new() { Key = "selector", Label = "Selektor", Kind = PropertyKind.Selector, Required = true, Aliases = ["xpath"] },
                new() { Key = "text", Label = "Text (alternativ)", Kind = PropertyKind.Text },
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
        new()
        {
            Type = "assert", DisplayName = "Prüfen / Assert", Category = "Kontrollfluss",
            Description = "Prüft eine Bedingung (wie if_then_else) und bricht den Pfad mit einer "
                + "Fehlermeldung ab, wenn sie NICHT erfüllt ist. Gültige Bedingungen: element_exists, "
                + "element_visible, element_text, page_url, page_title, page_contains, page_matches, "
                + "payload_equals, payload_contains. Vergleichswert in 'value'; DOM-Selektor bzw. bei "
                + "payload_* der Payload-Schlüssel in 'selector'.",
            Color = "#C62828", Icon = "✔",
            Example = "condition = element_exists, selector = .erfolg  →  Flow-Fehler, wenn .erfolg fehlt.",
            Properties =
            [
                new() { Key = "condition", Label = "Bedingung-Typ", Kind = PropertyKind.Dropdown, DefaultValue = "element_exists" },
                new() { Key = "selector", Label = "Selektor (bzw. Payload-Schlüssel)", Kind = PropertyKind.Selector },
                new() { Key = "value", Label = "Vergleichswert", Kind = PropertyKind.Text },
                new() { Key = "regex", Label = "Regex", Kind = PropertyKind.Boolean, DefaultValue = "false" },
                new() { Key = "negate", Label = "Negieren (fehlschlagen, wenn erfüllt)", Kind = PropertyKind.Boolean, DefaultValue = "false" },
                new() { Key = "message", Label = "Fehlermeldung (optional)", Kind = PropertyKind.Text },
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
            Type = "screenshot", DisplayName = "Screenshot", Category = "Erweitert",
            Description = "Speichert einen Screenshot der Seite oder eines Elements als PNG. Pfad leer = "
                + "Zeitstempel-Datei im Download-/Projektordner. Der Pfad landet im Payload unter 'screenshot_path'.",
            Color = "#4527A0", Icon = "📸",
            Example = "selector = .karte, path = beleg.png  →  PNG des Elements .karte.",
            Properties =
            [
                new() { Key = "path", Label = "Pfad (optional)", Kind = PropertyKind.FilePath },
                new() { Key = "selector", Label = "Element (Selektor, optional)", Kind = PropertyKind.Selector },
                new() { Key = "full_page", Label = "Ganze Seite (über den sichtbaren Bereich hinaus)", Kind = PropertyKind.Boolean, DefaultValue = "false" },
            ]
        },
        new()
        {
            Type = "download_stream", DisplayName = "Stream/Medien laden", Category = "Erweitert",
            Description = "Schneidet den Netzwerkverkehr für ein Zeitfenster mit, erkennt Medien-URLs "
                + "(eingebettete Videos/Audios, HLS .m3u8, DASH .mpd) und schreibt sie ins Payload (ctx_key). "
                + "Lädt direkte Dateien (mp4/mp3) per HTTP, Segment-Streams via ffmpeg (muss installiert/erreichbar "
                + "sein). Hinweis: Die Wiedergabe sollte beim Ausführen laufen; DRM-Streams sind nicht ladbar.",
            Color = "#4527A0", Icon = "🎬",
            Example = "wait_ms = 8000, download = true  →  erkennt den Stream und speichert ihn (stream_*.mp4).",
            Properties =
            [
                new() { Key = "wait_ms", Label = "Mitschnitt-Dauer (ms)", Kind = PropertyKind.Number, DefaultValue = "8000" },
                new() { Key = "download", Label = "Gefundenes Medium laden", Kind = PropertyKind.Boolean, DefaultValue = "true" },
                new() { Key = "filename", Label = "Dateiname (optional)", Kind = PropertyKind.Text },
                new() { Key = "ffmpeg_path", Label = "ffmpeg-Pfad (für HLS/DASH)", Kind = PropertyKind.Text, DefaultValue = "ffmpeg" },
                new() { Key = "ctx_key", Label = "Erkannte URLs → Payload-Schlüssel", Kind = PropertyKind.Text, DefaultValue = "media_urls" },
            ]
        },
        new()
        {
            Type = "use_session", DisplayName = "Sitzung verwenden", Category = "Erweitert",
            Description = "Prüft, ob eine gespeicherte Sitzung existiert (und – bei max_age_hours > 0 – nicht "
                + "zu alt ist). Falls ja, werden deren Cookies in den laufenden Browser geladen und der "
                + "Ausgang 'geladen' genommen; sonst 'keine Sitzung'. Damit: bei vorhandener Sitzung direkt "
                + "zur Seite navigieren, sonst den Login ausführen (und mit save_session sichern). "
                + "localStorage wird nicht wiederhergestellt — für cookie-basierte Logins.",
            Color = "#4527A0", Icon = "🔓",
            OutputPorts = 2, OutputLabels = ["geladen", "keine Sitzung"],
            Example = "Ausgang 'geladen' → Navigate zur Seite; Ausgang 'keine Sitzung' → Login + save_session.",
            Properties =
            [
                new() { Key = "path", Label = "Pfad (optional, leer = Einstellungs-Pfad)", Kind = PropertyKind.FilePath },
                new() { Key = "max_age_hours", Label = "Max. Alter in Stunden (0 = unbegrenzt)", Kind = PropertyKind.Number, DefaultValue = "0" },
            ]
        },
        new()
        {
            Type = "save_session", DisplayName = "Sitzung speichern", Category = "Erweitert",
            Description = "Speichert die aktuelle Sitzung (Cookies + localStorage) in eine Datei. "
                + "Wenn in den Einstellungen „Sitzung wiederverwenden\" aktiv ist, wird sie beim nächsten "
                + "Start automatisch geladen → Login und Captcha entfallen. Pfad leer = Einstellungs-Pfad "
                + "bzw. session.json im Projektordner.",
            Color = "#4527A0", Icon = "🔐",
            Example = "Nach erfolgreichem Login einfügen  →  nächster Lauf startet bereits angemeldet.",
            Properties =
            [
                new() { Key = "path", Label = "Pfad (optional)", Kind = PropertyKind.FilePath },
            ]
        },
        new()
        {
            Type = "page_function", DisplayName = "Function", Category = "Erweitert",
            Description = "Führt eine JavaScript-Funktion im Kontext der geöffneten Seite aus, um sie zu "
                + "bearbeiten oder Daten zu lesen. Ohne Selektor: payload => { … }; mit Selektor: "
                + "(element, payload) => { … }. Das Skript bekommt den aktuellen Payload als Argument. "
                + "Rückgabe: ist 'ctx_key' gesetzt, landet der Rückgabewert dort; sonst werden bei einem "
                + "zurückgegebenen Objekt dessen Felder in den Payload übernommen. Möglichkeiten: Hinweis/Banner "
                + "einblenden, Elemente hervorheben/entfernen, Werte zählen/berechnen, versteckte Werte lesen. "
                + "Hinweis: Der Code wird NICHT per {payload.x} ersetzt — nutze das payload-Argument.",
            Color = "#4527A0", Icon = "ƒ",
            Example = "code = payload => ({ anzahl: document.querySelectorAll('a').length })  →  {anzahl} im Payload.",
            Properties =
            [
                new() { Key = "code", Label = "JavaScript (payload => { … })", Kind = PropertyKind.Code, Required = true, Aliases = ["script"],
                        DefaultValue = "payload => {\n  // Beispiel: Hinweis einblenden\n  const d = document.createElement('div');\n  d.textContent = 'WebExStudio aktiv';\n  d.style.cssText = 'position:fixed;top:10px;right:10px;z-index:99999;background:#222;color:#fff;padding:8px 12px;border-radius:6px;font:14px sans-serif';\n  document.body.appendChild(d);\n  return { hinweis_gesetzt: true };\n}" },
                new() { Key = "selector", Label = "Element (Selektor, optional → (element, payload))", Kind = PropertyKind.Selector },
                new() { Key = "ctx_key", Label = "Rückgabe → Payload-Schlüssel (optional)", Kind = PropertyKind.Text },
                new() { Key = "merge", Label = "Rückgabe-Objekt in den Payload übernehmen", Kind = PropertyKind.Boolean, DefaultValue = "true" },
            ]
        },
        new()
        {
            Type = "captcha_guard", DisplayName = "CAPTCHA-Schutz", Category = "Erweitert",
            Description = "Erkennt CAPTCHA, klickt (optional) die erste „Ich bin kein Roboter\"-Checkbox "
                + "und wartet dann auf die (ggf. manuelle) Lösung. timeout_s = 0 bedeutet kein Zeitlimit "
                + "(wartet, bis gelöst, oder bis der Nutzer „Stopp\" drückt).",
            Color = "#4527A0", Icon = "🤖",
            Example = "auto_click = true, timeout_s = 120  →  klickt die Checkbox; ein folgendes Bild-Rätsel löst der Nutzer. timeout_s = 0 → wartet unbegrenzt.",
            Properties =
            [
                new() { Key = "timeout_s", Label = "Timeout (Sek., 0 = unbegrenzt)", Kind = PropertyKind.Number, DefaultValue = "120" },
                new() { Key = "auto_click", Label = "Erste Checkbox automatisch klicken", Kind = PropertyKind.Boolean, DefaultValue = "true" },
            ]
        },

        // ── KI ───────────────────────────────────────────────────────────────
        new()
        {
            Type = "ai_query", DisplayName = "KI-Abfrage", Category = "KI",
            Description = "Schickt den Inhalt der aktuellen Seite (Text oder HTML) zusammen mit einer Anweisung "
                + "an die KI und legt die Antwort im Payload ab. Beispiel: „Extrahiere alle Produktnamen und "
                + "Preise als JSON.\" Mit Selektor nur ein Element senden; mit json=true reine JSON-Antwort "
                + "erzwingen; max_chars begrenzt die gesendete Textmenge (Kosten/Token). Anbieter/Modell sind "
                + "optional pro Node wählbar (leer = Standard aus den Einstellungen; es wird der dort hinterlegte "
                + "API-Schlüssel verwendet). Erfordert eine konfigurierte KI (Einstellungen → KI).",
            Color = "#00838F", Icon = "🧠",
            Example = "prompt = Extrahiere alle Threads als JSON {titel,url}, ctx_key = daten, json = true  →  {daten}.",
            Properties =
            [
                new() { Key = "prompt", Label = "Anweisung an die KI", Kind = PropertyKind.MultilineText, Required = true },
                new() { Key = "ctx_key", Label = "Antwort → Payload-Schlüssel", Kind = PropertyKind.Text, DefaultValue = "ai_result" },
                new() { Key = "provider", Label = "Anbieter (leer = aus Einstellungen)", Kind = PropertyKind.Dropdown },
                new() { Key = "model", Label = "Modell (leer = Standard des Anbieters)", Kind = PropertyKind.Text },
                new() { Key = "selector", Label = "Nur dieses Element (Selektor, optional)", Kind = PropertyKind.Selector },
                new() { Key = "source", Label = "Inhalt (text/html)", Kind = PropertyKind.Dropdown, DefaultValue = "text" },
                new() { Key = "max_chars", Label = "Max. Zeichen", Kind = PropertyKind.Number, DefaultValue = "12000" },
                new() { Key = "json", Label = "JSON-Antwort erzwingen", Kind = PropertyKind.Boolean, DefaultValue = "false" },
            ]
        },

        // ── Anmerkung (reine Anzeige, keine Funktion) ─────────────────────────
        new()
        {
            Type = "note", DisplayName = "Notiz", Category = "Anmerkung",
            Description = "Zeigt Text auf der Arbeitsfläche an (keine Funktion). Stil 'heading' = große "
                + "Überschrift, 'comment' = kleiner Kommentar. Ersetzt die früheren caption/label-Nodes "
                + "(die als Alias erhalten bleiben).",
            Color = "#37474F", Icon = "🏷",
            InputPorts = 0, OutputPorts = 0,
            Example = "style = heading, text = Konfiguration  →  große Überschrift zur Gliederung des Flows.",
            Properties =
            [
                new() { Key = "style", Label = "Stil (heading/comment)", Kind = PropertyKind.Dropdown, DefaultValue = "comment" },
                new() { Key = "text", Label = "Text", Kind = PropertyKind.MultilineText, DefaultValue = "Kommentar" },
            ]
        },
        // Ersetzt durch 'note' — als versteckte Aliase erhalten, damit alte Flows gültig bleiben.
        new()
        {
            Type = "caption", DisplayName = "Caption / Überschrift", Category = "Anmerkung", Hidden = true,
            Description = "Zeigt eine große Überschrift auf der Arbeitsfläche an. Keine Funktion. (Ersetzt durch 'note'.)",
            Color = "#37474F", Icon = "🏷",
            InputPorts = 0, OutputPorts = 0,
            Properties = [ new() { Key = "text", Label = "Text", Kind = PropertyKind.Text, DefaultValue = "Überschrift" } ]
        },
        new()
        {
            Type = "label", DisplayName = "Label / Kommentar", Category = "Anmerkung", Hidden = true,
            Description = "Zeigt einen Kommentartext auf der Arbeitsfläche an. Keine Funktion. (Ersetzt durch 'note'.)",
            Color = "#37474F", Icon = "💬",
            InputPorts = 0, OutputPorts = 0,
            Properties = [ new() { Key = "text", Label = "Text", Kind = PropertyKind.MultilineText, DefaultValue = "Kommentar" } ]
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
