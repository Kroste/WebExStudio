namespace WebExStudio.Core.Models;

/// <summary>
/// A canvas tab. The main tab (IsSubFlow=false) contains wired nodes.
/// Sub-flow tabs (IsSubFlow=true) contain sequential nodes owned by a block node.
/// </summary>
public sealed class FlowTab
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Label { get; set; } = string.Empty;
    public bool IsSubFlow { get; set; }

    /// <summary>Unique identifier for a named subnode (e.g. dotted path). Empty for the main
    /// tab and for anonymous branch/body tabs owned by a block node.</summary>
    public string? Name { get; set; }

    /// <summary>ID of the block node that owns this sub-flow tab.</summary>
    public string? OwnerNodeId { get; set; }

    /// <summary>Slot name within the owner node: "then", "else", or "body".</summary>
    public string? Slot { get; set; }
}
