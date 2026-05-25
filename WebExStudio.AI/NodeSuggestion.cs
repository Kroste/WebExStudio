namespace WebExStudio.AI;

/// <summary>Ein KI-Vorschlag für den nächsten Node im Flow.</summary>
public sealed record NodeSuggestion
{
    public string Type { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public Dictionary<string, string> Config { get; init; } = new();
    public string Reason { get; init; } = string.Empty;
}

/// <summary>Ergebnis eines Node-Vorschlags.</summary>
public sealed class NodeSuggestionResult
{
    public NodeSuggestion? Suggestion { get; init; }
    public string RawResponse { get; init; } = string.Empty;
    public string? Error { get; init; }

    public bool Success => Error is null && Suggestion is not null;

    public static NodeSuggestionResult Failed(string error, string raw = "") =>
        new() { Error = error, RawResponse = raw };
}
