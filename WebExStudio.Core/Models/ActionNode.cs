using System.Text.Json;
using System.Text.Json.Serialization;
using WebExStudio.Core.Serialization;

namespace WebExStudio.Core.Models;

/// <summary>
/// Represents a single action in a flow. The JSON structure is backward-compatible
/// with the Python WebEX format: all action-specific fields are stored in Properties,
/// and the optional _ui field carries editor metadata (position, id).
/// </summary>
public sealed class ActionNode
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("_ui")]
    public UiMetadata? Ui { get; set; }

    /// <summary>
    /// All extra action-specific properties (e.g. url, selector, value, …).
    /// Stored as raw JsonElement to preserve unknown fields during round-trips.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Properties { get; set; }

    public string GetString(string key, string fallback = "") =>
        Properties != null && Properties.TryGetValue(key, out var v)
            ? v.ValueKind switch
            {
                JsonValueKind.String => v.GetString() ?? fallback,
                JsonValueKind.Number => v.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null or JsonValueKind.Undefined => fallback,
                _ => v.GetRawText(),  // Object / Array → JSON as string
            }
            : fallback;

    public bool GetBool(string key, bool fallback = false) =>
        Properties != null && Properties.TryGetValue(key, out var v)
            ? v.ValueKind == JsonValueKind.True || (v.ValueKind == JsonValueKind.String && bool.TryParse(v.GetString(), out var b) && b)
            : fallback;

    public List<ActionNode> GetSubActions(string key)
    {
        if (Properties == null || !Properties.TryGetValue(key, out var v))
            return [];
        if (v.ValueKind != JsonValueKind.Array) return [];
        return v.Deserialize<List<ActionNode>>(FlowSerializerOptions.Default) ?? [];
    }

    public List<string> GetStringArray(string key)
    {
        if (Properties == null || !Properties.TryGetValue(key, out var v)) return [];
        if (v.ValueKind == JsonValueKind.Array)
            return v.EnumerateArray()
                    .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : e.GetRawText())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
        var raw = v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.GetRawText();
        return raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    /// <summary>Ensures Ui is populated with a stable id and default position.</summary>
    public UiMetadata EnsureUi(double x = 0, double y = 0)
    {
        Ui ??= new UiMetadata { X = x, Y = y };
        return Ui;
    }
}
