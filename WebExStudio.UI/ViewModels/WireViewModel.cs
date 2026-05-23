namespace WebExStudio.UI.ViewModels;

/// <summary>
/// Represents a single wire connection between two node ports.
/// Wires are stored in FlowNode.Wires; this is the UI view model.
/// </summary>
public sealed class WireViewModel
{
    public string SourceNodeId { get; }
    public int OutputPort { get; }
    public string TargetNodeId { get; }
    public int InputPort { get; }

    public WireViewModel(string sourceNodeId, int outputPort, string targetNodeId, int inputPort = 0)
    {
        SourceNodeId = sourceNodeId;
        OutputPort = outputPort;
        TargetNodeId = targetNodeId;
        InputPort = inputPort;
    }
}
