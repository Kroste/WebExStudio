using WebExStudio.Engine;

namespace WebExStudio.UI.ViewModels;

public sealed class TraceEntryViewModel : ViewModelBase
{
    public TraceEntry Entry { get; }

    public string Time => Entry.Timestamp.ToString("HH:mm:ss.fff");
    public string ActionType => Entry.ActionType;
    public string TargetName => Entry.TargetName;
    public string StatusText => Entry.Status.ToString();
    public string? Message => Entry.Message ?? Entry.ErrorMessage;

    public string StatusColor => Entry.Status switch
    {
        ExecutionStatus.Running => "#FFC107",
        ExecutionStatus.Success => "#4CAF50",
        ExecutionStatus.Error => "#F44336",
        ExecutionStatus.Skipped => "#9E9E9E",
        _ => "#607D8B",
    };

    public bool HasMessage => !string.IsNullOrEmpty(Message);

    /// <summary>Node, zu dem dieser Eintrag gehört (für „zum Node springen").</summary>
    public string NodeId => Entry.NodeId;

    public bool IsError => Entry.Status == ExecutionStatus.Error;

    /// <summary>Ganze Zeile als Text (für „Kopieren").</summary>
    public string CopyText => $"{Time}\t{StatusText}\t{ActionType}\t{Message}".TrimEnd();

    /// <summary>Diagnose-Text zum Übertragen in den KI-Chat (Flow wird vom Chat ohnehin mitgesendet).</summary>
    public string DiagnosticText =>
        (IsError
            ? $"Beim Ausführen des Nodes „{ActionType}“"
            : $"Frage zum Node „{ActionType}“ (Status {StatusText})")
        + (string.IsNullOrEmpty(NodeId) ? "" : $" (id: {NodeId})")
        + (IsError ? " ist ein Fehler aufgetreten." : ".")
        + (HasMessage ? $"\nMeldung: {Message}" : "")
        + "\n\nBitte hilf mir, das im aktuellen Flow zu beheben.";

    public TraceEntryViewModel(TraceEntry entry) => Entry = entry;
}
