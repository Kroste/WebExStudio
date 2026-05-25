using System.Text.Json;
using NLog;
using WebExStudio.Core.Models;
using WebExStudio.Core.Serialization;

namespace WebExStudio.AI;

/// <summary>
/// Schlägt anhand des aktuellen Flows und eines Anker-Nodes den nächsten sinnvollen Node vor.
/// Provider-unabhängig; das Parsen/Validieren ist ohne Netzwerk testbar.
/// </summary>
public sealed class NodeSuggester(ILlmClient client)
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new StringValueDictionaryConverter() },
    };

    public async Task<NodeSuggestionResult> SuggestAsync(
        string flowJson, string anchorId, string anchorType, string? hints = null, CancellationToken ct = default)
    {
        string raw;
        try
        {
            Log.Info("Node-Vorschlag via {0} (nach {1})…", client.Name, anchorId);
            raw = await client.CompleteAsync(
                PromptBuilder.BuildSuggestSystemPrompt(hints),
                PromptBuilder.BuildSuggestUserPrompt(flowJson, anchorId, anchorType),
                ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Log.Warn("Node-Vorschlag fehlgeschlagen: {0}", ex.Message);
            return NodeSuggestionResult.Failed($"KI-Anfrage fehlgeschlagen: {ex.Message}");
        }
        return Parse(raw);
    }

    /// <summary>Extrahiert/validiert einen Vorschlag aus beliebigem Text. Ohne Netzwerk.</summary>
    public static NodeSuggestionResult Parse(string raw)
    {
        var json = JsonExtractor.Extract(raw);
        if (string.IsNullOrWhiteSpace(json))
            return NodeSuggestionResult.Failed("Leere Modellantwort.", raw);

        try
        {
            var s = JsonSerializer.Deserialize<NodeSuggestion>(json, Options);
            if (s is null || string.IsNullOrWhiteSpace(s.Type))
                return NodeSuggestionResult.Failed("Kein Node-Typ in der Antwort.", raw);
            if (NodeCatalog.Get(s.Type) is null)
                return NodeSuggestionResult.Failed($"Unbekannter Node-Typ '{s.Type}'.", raw);
            return new NodeSuggestionResult { Suggestion = s, RawResponse = raw };
        }
        catch (JsonException ex)
        {
            return NodeSuggestionResult.Failed($"Antwort war kein gültiger Vorschlag: {ex.Message}", raw);
        }
    }
}
