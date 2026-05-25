using System.Text.Json;
using WebExStudio.Core.Models;

namespace WebExStudio.Core.Ai;

/// <summary>
/// Leitet aus dem <see cref="NodeCatalog"/> eine kompakte Schema-Beschreibung aller Node-Typen
/// ab. Einzige Quelle der Wahrheit bleibt der Katalog — das Schema wird daraus erzeugt, nicht
/// dupliziert. Wird in den KI-Prompt eingebettet, damit das Modell gültige Flows produziert.
/// </summary>
public static class NodeSchemaExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Alle Node-Typen als strukturiertes Schema.</summary>
    public static IReadOnlyList<NodeSchema> Export() =>
        NodeCatalog.All.Select(ToSchema).ToList();

    /// <summary>Das Schema als JSON-Text (für den Prompt).</summary>
    public static string ToJson() =>
        JsonSerializer.Serialize(Export(), JsonOptions);

    private static NodeSchema ToSchema(NodeDefinition d)
    {
        var outputs = Enumerable.Range(0, d.OutputPorts)
            .Select(i => i < d.OutputLabels.Length ? d.OutputLabels[i]
                       : d.OutputPorts == 1 ? "weiter"
                       : $"out{i}")
            .ToList();

        var props = d.Properties
            .Select(p => new NodePropertySchema(
                p.Key, p.Label, p.Kind.ToString(), p.Required, p.DefaultValue,
                p.Aliases is { Length: > 0 } ? p.Aliases : null))
            .ToList();

        return new NodeSchema(
            d.Type, d.Category, d.Description, d.Example, d.InputPorts, outputs, props);
    }
}
