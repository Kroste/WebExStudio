namespace WebExStudio.Core.Validation;

/// <summary>Schweregrad eines Validierungsbefunds.</summary>
public enum FlowIssueSeverity
{
    /// <summary>Der Flow ist fehlerhaft und läuft so nicht (zuverlässig).</summary>
    Error,
    /// <summary>Auffällig/verdächtig, evtl. aber gewollt.</summary>
    Warning,
}

/// <summary>Ein einzelner Befund der Flow-Validierung.</summary>
/// <param name="Severity">Schweregrad.</param>
/// <param name="Code">Stabiler, maschinenlesbarer Kennzeichner (z. B. <c>unknown-type</c>).</param>
/// <param name="Message">Deutsche, für Menschen lesbare Beschreibung.</param>
/// <param name="NodeId">Betroffener Node, falls zutreffend.</param>
/// <param name="TabId">Betroffener Tab, falls zutreffend.</param>
public sealed record FlowIssue(
    FlowIssueSeverity Severity,
    string Code,
    string Message,
    string? NodeId = null,
    string? TabId = null);

/// <summary>Ergebnis einer Flow-Validierung — eine Sammlung von Befunden.</summary>
public sealed class FlowValidationResult
{
    public List<FlowIssue> Issues { get; } = [];

    /// <summary>True, wenn keine Fehler (Warnungen sind erlaubt) vorliegen.</summary>
    public bool IsValid => Issues.All(i => i.Severity != FlowIssueSeverity.Error);

    public IEnumerable<FlowIssue> Errors => Issues.Where(i => i.Severity == FlowIssueSeverity.Error);
    public IEnumerable<FlowIssue> Warnings => Issues.Where(i => i.Severity == FlowIssueSeverity.Warning);

    internal void Add(FlowIssueSeverity severity, string code, string message, string? nodeId = null, string? tabId = null) =>
        Issues.Add(new FlowIssue(severity, code, message, nodeId, tabId));

    /// <summary>Kurze Zusammenfassung für Logs/Anzeige.</summary>
    public override string ToString() =>
        Issues.Count == 0
            ? "Flow gültig — keine Befunde."
            : $"{Errors.Count()} Fehler, {Warnings.Count()} Warnung(en):" +
              string.Concat(Issues.Select(i => $"\n  [{i.Severity}] {i.Code}: {i.Message}"));
}
