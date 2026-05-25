using System.Text.Json;
using NLog;
using WebExStudio.Core.Serialization;
using WebExStudio.Core.Validation;

namespace WebExStudio.AI;

/// <summary>
/// Erzeugt aus einer natürlichsprachlichen Beschreibung einen Flow: baut den Prompt, ruft das
/// Modell über <see cref="ILlmClient"/>, extrahiert und parst das JSON und validiert es mit dem
/// <see cref="FlowValidator"/>. Der Generator ist provider-unabhängig und ohne Netzwerk testbar.
/// </summary>
public sealed class FlowGenerator(ILlmClient client)
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public async Task<FlowGenerationResult> GenerateAsync(string description, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(description))
            return FlowGenerationResult.Failed("Keine Beschreibung angegeben.");

        string raw;
        try
        {
            Log.Info("Generiere Flow via {0}…", client.Name);
            raw = await client.CompleteAsync(
                PromptBuilder.BuildSystemPrompt(),
                PromptBuilder.BuildUserPrompt(description),
                ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Log.Warn("KI-Anfrage fehlgeschlagen: {0}", ex.Message);
            return FlowGenerationResult.Failed($"KI-Anfrage fehlgeschlagen: {ex.Message}");
        }

        var json = JsonExtractor.Extract(raw);
        if (string.IsNullOrWhiteSpace(json))
            return FlowGenerationResult.Failed("Leere Modellantwort.", raw);

        try
        {
            var doc = FlowSerializer2.Deserialize(json);
            var validation = FlowValidator.Validate(doc);
            Log.Info("Flow generiert: {0} Nodes, gültig={1}", doc.Nodes.Count, validation.IsValid);
            return new FlowGenerationResult
            {
                Document = doc,
                Validation = validation,
                RawResponse = raw,
            };
        }
        catch (JsonException ex)
        {
            Log.Warn("KI-Antwort war kein gültiges JSON: {0}", ex.Message);
            return FlowGenerationResult.Failed($"Antwort war kein gültiges Flow-JSON: {ex.Message}", raw);
        }
    }
}
