using WebExStudio.Core.Ai;

namespace WebExStudio.AI;

/// <summary>Baut System- und Benutzer-Prompt für die Flow-Generierung.</summary>
public static class PromptBuilder
{
    /// <summary>
    /// Systemkontext: Aufgabe, Flow-JSON-Format, Node-Katalog (aus dem Schema) und harte Regeln.
    /// </summary>
    public static string BuildSystemPrompt() =>
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
        """;

    /// <summary>Benutzer-Prompt: die natürliche Beschreibung des gewünschten Flows.</summary>
    public static string BuildUserPrompt(string description) =>
        $"""
        Erzeuge einen Flow für folgende Aufgabe:

        {description}
        """;
}
