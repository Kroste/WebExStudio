namespace WebExStudio.Core.Models;

/// <summary>Root document for the v2 Node-RED style flow format.</summary>
public sealed class FlowDocument2
{
    public int Version { get; set; } = 2;
    public List<FlowTab> Tabs { get; set; } = [];
    public List<FlowNode> Nodes { get; set; } = [];
    public string? FilePath { get; set; }

    /// <summary>Returns nodes belonging to the given tab, sorted by seqIndex for sub-flow tabs.</summary>
    public IEnumerable<FlowNode> GetNodes(string tabId) =>
        Nodes.Where(n => n.TabId == tabId).OrderBy(n => n.SeqIndex);

    /// <summary>Builds a set of node IDs that have at least one incoming wire (for the given tab).</summary>
    public HashSet<string> BuildIncomingSet(string tabId)
    {
        var set = new HashSet<string>();
        foreach (var node in Nodes.Where(n => n.TabId == tabId))
            foreach (var port in node.Wires)
                foreach (var targetId in port)
                    set.Add(targetId);
        return set;
    }

    public FlowTab? GetTab(string tabId) =>
        Tabs.FirstOrDefault(t => t.Id == tabId);

    public FlowNode? GetNode(string nodeId) =>
        Nodes.FirstOrDefault(n => n.Id == nodeId);
}
