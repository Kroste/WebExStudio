namespace WebExStudio.Engine;

public enum ExecutionStatus { Running, Success, Error, Skipped }

public sealed record TraceEntry(
    string NodeId,
    string ActionType,
    ExecutionStatus Status,
    DateTime Timestamp,
    string TargetName,
    IReadOnlyDictionary<string, string> ContextSnapshot,
    string? Message = null,
    string? ErrorMessage = null
);
