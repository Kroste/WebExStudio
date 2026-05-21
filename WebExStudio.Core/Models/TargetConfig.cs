using System.Text.Json.Serialization;

namespace WebExStudio.Core.Models;

public sealed class TargetConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("actions_file")]
    public string ActionsFile { get; set; } = string.Empty;

    /// <summary>Target-specific context variables (placeholder substitution).</summary>
    [JsonPropertyName("ctx")]
    public Dictionary<string, string> Ctx { get; set; } = [];

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}
