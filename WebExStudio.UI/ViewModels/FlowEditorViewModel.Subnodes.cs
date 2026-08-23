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
    // ── Subnode management (Node-RED style) ────────────────────────────────────

    /// <summary>Creates a new named, standalone subnode and opens it.</summary>
    public FlowTabViewModel? CreateSubnode(string name, string label)
    {
        if (Document is null) return null;
        name = name.Trim();
        if (string.IsNullOrEmpty(name) || Document.GetTabByName(name) is not null)
        {
            Log.Warn("CreateSubnode: Name leer oder bereits vergeben: {0}", name);
            return null;
        }
        var tab = new FlowTab
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = name,
            Label = string.IsNullOrWhiteSpace(label) ? name : label.Trim(),
            IsSubFlow = true,
        };
        Document.Tabs.Add(tab);
        var tabVm = new FlowTabViewModel(tab);
        Tabs.Add(tabVm);
        Subnodes.Add(tabVm);
        MarkDirty();
        this.RaisePropertyChanged(nameof(SubnodeNames));
        OpenTab(tabVm);
        return tabVm;
    }

    public void RenameSubnode(FlowTabViewModel tab, string name, string label)
    {
        if (Document is null || !tab.IsSubnode) return;
        name = name.Trim();
        if (string.IsNullOrEmpty(name)) return;
        var clash = Document.GetTabByName(name);
        if (clash is not null && clash.Id != tab.Id)
        {
            Log.Warn("RenameSubnode: Name bereits vergeben: {0}", name);
            return;
        }
        tab.Model.Name = name;
        tab.Model.Label = string.IsNullOrWhiteSpace(label) ? name : label.Trim();
        tab.NotifyLabelChanged();
        MarkDirty();
        this.RaisePropertyChanged(nameof(SubnodeNames));
    }

    public void DeleteSubnode(FlowTabViewModel tab)
    {
        if (Document is null || !tab.IsSubnode) return;
        var refs = Document.Nodes.Count(n => n.Type == "call" && n.Get("target") == tab.Name);
        if (refs > 0)
            Log.Warn("DeleteSubnode: '{0}' wird noch von {1} call-Node(s) referenziert", tab.Name, refs);

        var subNodes = Document.Nodes.Where(n => n.TabId == tab.Id).ToList();
        foreach (var sn in subNodes) Document.Nodes.Remove(sn);
        Document.Tabs.Remove(tab.Model);
        Tabs.Remove(tab);
        Subnodes.Remove(tab);
        OpenTabs.Remove(tab);
        if (_activeTab == tab)
            SwitchTab(Tabs.FirstOrDefault(t => !t.IsSubFlow));
        MarkDirty();
        this.RaisePropertyChanged(nameof(SubnodeNames));
    }
}
