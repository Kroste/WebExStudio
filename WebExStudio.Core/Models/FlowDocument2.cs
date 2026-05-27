namespace WebExStudio.Core.Models;

/// <summary>Root document for the v2 Node-RED style flow format.</summary>
public sealed class FlowDocument2
{
    public int Version { get; set; } = 2;
    public List<FlowTab> Tabs { get; set; } = [];
    public List<FlowNode> Nodes { get; set; } = [];
    public List<FlowGroup> Groups { get; set; } = [];
    public string? FilePath { get; set; }

    /// <summary>
    /// Verschlüsselter Anmeldedaten-Tresor dieses Flows (Base64 eines AES-256-GCM-Blobs, Schlüssel via
    /// PBKDF2 aus dem Master-Passwort). Opak — nur <c>WebExStudio.Core.Credentials.CredentialVault</c>
    /// liest/schreibt ihn. So liegen Flow und (verschlüsselte) Passwörter zusammen; Flow A enthält nie
    /// die Secrets von Flow B. Null/leer = kein Tresor.
    /// </summary>
    public string? Credentials { get; set; }

    /// <summary>Visual groups on the given tab.</summary>
    public IEnumerable<FlowGroup> GetGroups(string tabId) =>
        Groups.Where(g => g.TabId == tabId);

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

    /// <summary>Finds a named subnode tab by its unique Name.</summary>
    public FlowTab? GetTabByName(string name) =>
        Tabs.FirstOrDefault(t => t.Name == name);

    /// <summary>All named, standalone subnodes (reusable sub-flows referenced by call).</summary>
    public IEnumerable<FlowTab> Subnodes =>
        Tabs.Where(t => t.IsSubFlow && t.OwnerNodeId is null && !string.IsNullOrEmpty(t.Name));

    public FlowNode? GetNode(string nodeId) =>
        Nodes.FirstOrDefault(n => n.Id == nodeId);
}
