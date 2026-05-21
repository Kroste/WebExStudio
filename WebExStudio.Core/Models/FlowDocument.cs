using System.Text.Json.Serialization;

namespace WebExStudio.Core.Models;

/// <summary>
/// Root document of a flow file. Compatible with the Python WebEX JSON format.
/// </summary>
public sealed class FlowDocument
{
    [JsonPropertyName("actions")]
    public List<ActionNode> Actions { get; set; } = [];

    /// <summary>File path this document was loaded from (not serialized).</summary>
    [JsonIgnore]
    public string? FilePath { get; set; }

    [JsonIgnore]
    public string DisplayName => FilePath is not null
        ? Path.GetFileNameWithoutExtension(FilePath)
        : "Unbenannt";
}
