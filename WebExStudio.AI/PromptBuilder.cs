using WebExStudio.Core.Ai;
using WebExStudio.Core.Localization;

namespace WebExStudio.AI;

/// <summary>Baut System- und Benutzer-Prompt für die KI-Funktionen.</summary>
public static class PromptBuilder
{
    /// <summary>
    /// Weist die KI an, in der aktuell eingestellten UI-Sprache zu antworten (Erklärungen/Chat/Begründungen).
    /// Ohne diese Regel antwortet das Modell oft in der Prompt-Sprache (Deutsch). Node-`type`/config bleiben
    /// davon unberührt — die sind ohnehin sprachunabhängige Bezeichner.
    /// </summary>
    private static string AnswerLanguageRule()
    {
        var loc = Loc.Instance;
        return $"WICHTIG: Antworte ausschließlich in dieser Sprache: {loc.NameOf(loc.Language)} ({loc.Language}). "
             + "Das gilt für allen Erklär-/Fließtext (Node-Typen und config-Schlüssel bleiben unverändert).";
    }

    /// <summary>Hängt — falls vorhanden — die vom Nutzer gepflegten Hinweise/Problemlösungen an.</summary>
    private static string WithHints(string prompt, string? hints) =>
        string.IsNullOrWhiteSpace(hints)
            ? prompt
            : prompt + $$"""



                BEKANNTE HINWEISE / PROBLEMLÖSUNGEN (unbedingt beachten):
                {{hints!.Trim()}}
                """;

    /// <summary>
    /// Systemkontext: Aufgabe, Flow-JSON-Format, Node-Katalog (aus dem Schema) und harte Regeln.
    /// </summary>
    public static string BuildSystemPrompt(string? hints = null) =>
        WithHints(
            $$"""
            Du bist ein Assistent, der Web-Automatisierungs-Flows für die Anwendung „WebExStudio“
            erzeugt. Ein Flow ist EIN JSON-Objekt im folgenden Format (Version 2):

            {
              "version": 2,
              "tabs": [
                { "id": "main", "label": "Main", "isSubFlow": false }
              ],
              "nodes": [
                {
                  "id": "<eindeutige kurze id>",
                  "type": "<einer der unten gelisteten Typen>",
                  "tabId": "main",
                  "label": "<frei wählbare Bezeichnung, optional>",
                  "config": { "<schlüssel>": "<wert als string>" },
                  "wires": [ ["<ziel-node-id>"] ]
                }
              ]
            }

            REGELN (zwingend):
            - Antworte AUSSCHLIESSLICH mit dem JSON-Objekt, ohne Erklärtext, ohne Markdown.
            - Verwende NUR Node-Typen aus dem Katalog unten. Erfinde keine Typen oder config-Schlüssel.
            - Setze alle als "required" markierten config-Felder. Alle config-Werte sind Strings
              (auch Zahlen/Booleans, z. B. "true", "5").
            - "wires[portIndex]" ist die Liste der Ziel-Node-IDs an diesem Ausgang. Verzweigungen
              (if_then_else) nutzen wires[0]=then, wires[1]=else; Schleifen (foreach, for_range,
              get_links) nutzen wires[0]=Schleifenkörper, wires[1]=danach. Siehe "outputs" je Typ.
            - Verbindungen bleiben innerhalb eines Tabs. Wiederverwendbare Teilabläufe als benannte
              Subnodes (eigener Tab mit "isSubFlow": true und eindeutigem "name") und per "call"
              (config.target = subnode-name) aufrufen.
            - Verkette Schritte über "wires"; nutze {payload.schlüssel} bzw. {schlüssel} als Platzhalter.
            - Lass x/y weg — die Anordnung passiert automatisch.
            - Beginne sinnvoll mit einem "function"- oder "goto"-Node.

            VERFÜGBARE NODE-TYPEN (Katalog):
            {{NodeSchemaExporter.ToJson()}}
            """, hints);

    /// <summary>Benutzer-Prompt: die natürliche Beschreibung des gewünschten Flows.</summary>
    public static string BuildUserPrompt(string description) =>
        $"""
        Erzeuge einen Flow für folgende Aufgabe:

        {description}
        """;

    /// <summary>
    /// Systemkontext für den Chat-Assistenten: hilft rund um WebExStudio, kennt den Node-Katalog
    /// und liefert auf Wunsch einen kompletten Flow als ```json-Block (den das Chat-Fenster
    /// erkennen und in den Editor laden kann). Ist <paramref name="currentFlowJson"/> gesetzt,
    /// wird der aktuelle Stand des Editors mitgegeben, damit Änderungen darauf aufbauen.
    /// </summary>
    public static string BuildChatSystemPrompt(string? currentFlowJson = null, string? hints = null)
    {
        var prompt =
            $$"""
            Du bist der KI-Assistent der Anwendung „WebExStudio“ (visuelle Web-Automatisierungs-Flows,
            Ausführung über Playwright). Hilf dem Nutzer: erkläre Konzepte, schlage Nodes
            und Verbindungen vor und beantworte Fragen knapp und konkret.
            {{AnswerLanguageRule()}}

            Wenn der Nutzer darum bittet, einen Flow zu erstellen oder zu ändern, gib einen VOLLSTÄNDIGEN
            Flow als JSON in einem ```json-Codeblock aus (Format unten) — bei Änderungen den GESAMTEN
            angepassten Flow, nicht nur das Delta. Eine kurze Erklärung davor/danach ist erlaubt.
            Verwende nur Node-Typen aus dem Katalog, setze alle "required"-Felder, alle config-Werte
            sind Strings, lass x/y weg.

            Flow-Format (Version 2):
            { "version": 2,
              "tabs": [ { "id": "main", "label": "Main", "isSubFlow": false } ],
              "nodes": [ { "id": "...", "type": "...", "tabId": "main", "config": { }, "wires": [ ["zielId"] ] } ] }

            Port-Semantik: if_then_else → wires[0]=then, wires[1]=else; Schleifen (foreach/for_range/
            get_links) → wires[0]=Körper, wires[1]=danach. Subnodes = eigener Tab (isSubFlow:true,
            eindeutiger name), Aufruf per "call" (config.target = name).

            VERFÜGBARE NODE-TYPEN (Katalog):
            {{NodeSchemaExporter.ToJson()}}
            """;

        if (!string.IsNullOrWhiteSpace(currentFlowJson))
            prompt += $$"""


                AKTUELLER FLOW IM EDITOR (aktueller Stand — beziehe dich hierauf; bei Änderungen den
                VOLLSTÄNDIGEN geänderten Flow zurückgeben):
                {{currentFlowJson}}
                """;

        return WithHints(prompt, hints);
    }

    /// <summary>Systemkontext zum Erklären eines bestehenden Flows in verständlicher Prosa.</summary>
    public static string BuildExplainSystemPrompt(string? hints = null) =>
        WithHints(
            $$"""
            Du erklärst Web-Automatisierungs-Flows der Anwendung „WebExStudio“ verständlich.
            {{AnswerLanguageRule()}}
            Der Nutzer schickt dir einen Flow als JSON. Erkläre:
            1. was der Flow insgesamt tut (1–2 Sätze Überblick),
            2. den Ablauf Schritt für Schritt entlang der Verbindungen (wires), inkl. Verzweigungen
               (if then/else) und Schleifen (foreach/for_range) sowie aufgerufener Subnodes (call),
            3. auffällige Risiken oder fehlende Schritte, falls vorhanden.
            Nutze die Bezeichnungen (label) der Nodes, wenn vorhanden. Antworte als Fließtext/Aufzählung,
            NICHT als JSON. Beziehe dich auf den Node-Katalog für die Bedeutung der Typen.

            NODE-KATALOG:
            {{NodeSchemaExporter.ToJson()}}
            """, hints);

    /// <summary>Systemkontext für den Vorschlag des nächsten Nodes (striktes JSON-Objekt).</summary>
    public static string BuildSuggestSystemPrompt(string? hints = null) =>
        WithHints(
            $$"""
            Du schlägst den NÄCHSTEN sinnvollen Node für einen WebExStudio-Flow vor.
            {{AnswerLanguageRule()}}
            Antworte AUSSCHLIESSLICH mit EINEM JSON-Objekt in genau dieser Form:
            { "type": "<node-typ aus dem Katalog>", "label": "<kurze Bezeichnung>",
              "config": { "<schlüssel>": "<wert>" }, "reason": "<kurze Begründung>" }

            Regeln: nur Typen aus dem Katalog; setze sinnvolle Pflicht-config-Werte (alle als String);
            genau EIN Node; keine Erklärung außerhalb des JSON.

            NODE-KATALOG:
            {{NodeSchemaExporter.ToJson()}}
            """, hints);

    /// <summary>Benutzer-Prompt für den Node-Vorschlag: Flow + Anker-Node.</summary>
    public static string BuildSuggestUserPrompt(string flowJson, string anchorId, string anchorType) =>
        $"""
        Aktueller Flow:
        {flowJson}

        Schlage den nächsten Node vor, der NACH dem Node mit id="{anchorId}" (Typ {anchorType})
        angehängt werden soll.
        """;
}
