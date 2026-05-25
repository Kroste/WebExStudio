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

        return Parse(raw);
    }

    /// <summary>
    /// Extrahiert ein Flow-JSON aus beliebigem Text (z. B. einer Chat-Antwort), parst und
    /// validiert es. Ohne Netzwerk — auch vom Chat-Fenster genutzt.
    /// </summary>
    public static FlowGenerationResult Parse(string raw)
    {
        var json = JsonExtractor.Extract(raw);
        if (string.IsNullOrWhiteSpace(json))
            return FlowGenerationResult.Failed("Leere Modellantwort.", raw);

        try
        {
            var doc = FlowSerializer2.Deserialize(json);
            var validation = FlowValidator.Validate(doc);
            return new FlowGenerationResult
            {
                Document = doc,
                Validation = validation,
                RawResponse = raw,
            };
        }
        catch (JsonException ex)
        {
            return FlowGenerationResult.Failed($"Antwort war kein gültiges Flow-JSON: {ex.Message}", raw);
        }
    }
}
