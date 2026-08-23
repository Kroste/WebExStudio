using System.Collections.ObjectModel;
using NLog;
using ReactiveUI;
using WebExStudio.Core.Localization;
using WebExStudio.Core.Models;
using WebExStudio.Core.Serialization;

namespace WebExStudio.UI.ViewModels;

// Teil von FlowEditorViewModel — Aufteilung nach Verantwortlichkeit (siehe FlowEditorViewModel.cs).
public partial class FlowEditorViewModel
{
    // ── Groups ─────────────────────────────────────────────────────────────────

    /// <summary>Groups the currently selected nodes into a visual group on the active tab.</summary>
    public GroupViewModel? CreateGroupFromSelection(string label = "Gruppe")
    {
        if (Document is null || _activeTab is null || SelectedNodes.Count == 0) return null;
        var g = new FlowGroup
        {
            Id = NewId(), TabId = _activeTab.Id, Label = label,
            NodeIds = SelectedNodes.Select(n => n.Id).ToList(),
        };
        Document.Groups.Add(g);
        Groups.Add(new GroupViewModel(g));
        MarkDirty();
        this.RaisePropertyChanged(nameof(Groups));
        return Groups[^1];
    }

    public void Ungroup(GroupViewModel group)
    {
        if (Document is null) return;
        Document.Groups.Remove(group.Model);
        Groups.Remove(group);
        MarkDirty();
        this.RaisePropertyChanged(nameof(Groups));
    }

    public void RenameGroup(GroupViewModel group, string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return;
        group.Model.Label = label.Trim();
        group.NotifyLabelChanged();
        MarkDirty();
    }

    /// <summary>
    /// Moves all of a group's nodes into a new named subnode and replaces them on the
    /// original tab with a single <c>call</c> node. External wires are re-routed to/from the
    /// call node (incoming → call input, outgoing → call output). Returns the new subnode tab.
    /// </summary>
    public FlowTabViewModel? ExtractGroupToSubnode(GroupViewModel group, string subnodeName, string label)
    {
        if (Document is null) return null;
        subnodeName = subnodeName.Trim();
        if (string.IsNullOrEmpty(subnodeName) || Document.GetTabByName(subnodeName) is not null)
        {
            Log.Warn("ExtractGroupToSubnode: Name leer oder vergeben: {0}", subnodeName);
            return null;
        }

        var tabId = group.Model.TabId;
        var memberIds = group.Model.NodeIds.ToHashSet();
        var members = Document.Nodes.Where(n => n.TabId == tabId && memberIds.Contains(n.Id)).ToList();
        if (members.Count == 0) return null;

        // New subnode tab.
        var sub = new FlowTab
        {
            Id = NewId(), Name = subnodeName,
            Label = string.IsNullOrWhiteSpace(label) ? subnodeName : label.Trim(),
            IsSubFlow = true,
        };
        Document.Tabs.Add(sub);

        // Call node placed at the group's top-left.
        var call = new FlowNode
        {
            Id = NewId(), Type = "call", TabId = tabId,
            X = members.Min(m => m.X), Y = members.Min(m => m.Y),
            Config = new() { ["target"] = subnodeName }, Wires = [[]],
        };

        // External incoming wires (outside → member) now point at the call node.
        foreach (var ext in Document.Nodes.Where(n => n.TabId == tabId && !memberIds.Contains(n.Id)))
            foreach (var port in ext.Wires)
                for (int i = 0; i < port.Count; i++)
                    if (memberIds.Contains(port[i])) port[i] = call.Id;

        // External outgoing wires (member → outside) now emit from the call node's output.
        foreach (var m in members)
            foreach (var port in m.Wires)
                foreach (var t in port.Where(t => !memberIds.Contains(t)).ToList())
                {
                    port.Remove(t);
                    if (!call.Wires[0].Contains(t)) call.Wires[0].Add(t);
                }

        // Move members into the subnode; add the call node; drop the group.
        foreach (var m in members) m.TabId = sub.Id;
        Document.Nodes.Add(call);
        Document.Groups.Remove(group.Model);

        // De-duplicate any wire lists touched above.
        foreach (var n in Document.Nodes)
            for (int p = 0; p < n.Wires.Count; p++)
                n.Wires[p] = n.Wires[p].Distinct().ToList();

        MarkDirty();

        // Rebuild the view, return to the original tab (call node now visible), expose the subnode.
        LoadDocument(Document);
        var orig = Tabs.FirstOrDefault(t => t.Id == tabId);
        if (orig is not null) OpenTab(orig);
        return Tabs.FirstOrDefault(t => t.Id == sub.Id);
    }

    private static string NewId() => Guid.NewGuid().ToString("N")[..8];
}
