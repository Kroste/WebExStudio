namespace WebExStudio.Core.Models;

/// <summary>
/// A single node in the flow graph. Lives on a specific tab (main or sub-flow).
/// On main-canvas tabs: wires define execution order (N:N).
/// On sub-flow tabs: seqIndex defines execution order (sequential).
/// </summary>
public sealed class FlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Type { get; set; } = string.Empty;
    public string TabId { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public Dictionary<string, string> Config { get; set; } = new();

    /// <summary>Output wires: wires[outputPortIndex] = list of target node IDs.</summary>
    public List<List<string>> Wires { get; set; } = [[]];

    /// <summary>Execution order within a sequential sub-flow tab.</summary>
    public int SeqIndex { get; set; }

    public string Get(string key, string fallback = "") =>
        Config.TryGetValue(key, out var v) ? v : fallback;

    public bool GetBool(string key, bool fallback = false) =>
        Config.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : fallback;

    public List<string> GetStringList(string key)
    {
        if (!Config.TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v))
            return [];
        if (v.TrimStart().StartsWith('['))
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(v) ?? [];
            }
            catch { /* fall through */ }
        }
        return v.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}
