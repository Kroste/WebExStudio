using System.Text.Json.Serialization;

namespace WebExStudio.Core.Models;

public sealed class RunConfig
{
    [JsonPropertyName("browser")]
    public string Browser { get; set; } = "chromium";

    [JsonPropertyName("headless")]
    public bool Headless { get; set; } = false;

    [JsonPropertyName("slow_mo_ms")]
    public int SlowMoMs { get; set; } = 0;

    [JsonPropertyName("timeout_ms")]
    public int TimeoutMs { get; set; } = 30000;

    [JsonPropertyName("download_dir")]
    public string DownloadDir { get; set; } = string.Empty;

    [JsonPropertyName("project_dir")]
    public string ProjectDir { get; set; } = string.Empty;

    /// <summary>Global context variables merged with every target's ctx.</summary>
    [JsonPropertyName("ctx")]
    public Dictionary<string, string> Ctx { get; set; } = [];
}
