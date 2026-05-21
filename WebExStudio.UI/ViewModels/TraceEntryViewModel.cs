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

    public TraceEntryViewModel(TraceEntry entry) => Entry = entry;
}
