using System.Collections.ObjectModel;
using NLog;
using ReactiveUI;
using WebExStudio.Core.Localization;
using WebExStudio.Core.Models;
using WebExStudio.Core.Serialization;

namespace WebExStudio.UI.ViewModels;

// Teil von FlowEditorViewModel — Aufteilung nach Verantwortlichkeit (siehe FlowEditorViewModel.cs).
partial class FlowEditorViewModel
{
    // ── Tab operations ────────────────────────────────────────────────────────

    public void SwitchTab(FlowTabViewModel? tab)
    {
        if (tab is null || tab == _activeTab) return;
        Log.Debug("Tab wechseln: {0}", tab.Label);
        ClearSelection();
        _activeTab = tab;
        this.RaisePropertyChanged(nameof(ActiveTab));
        this.RaisePropertyChanged(nameof(Nodes));
        RefreshWires();
        RefreshGroups();
    }

    public void RefreshGroups()
    {
        Groups.Clear();
        if (_activeTab is not null && Document is not null)
            foreach (var g in Document.GetGroups(_activeTab.Id))
                Groups.Add(new GroupViewModel(g));
        this.RaisePropertyChanged(nameof(Groups));
    }

    /// <summary>Opens a tab in the tab bar (if not already) and switches to it.</summary>
    public void OpenTab(FlowTabViewModel? tab)
    {
        if (tab is null) return;
        if (!OpenTabs.Contains(tab)) OpenTabs.Add(tab);
        SwitchTab(tab);
    }

    /// <summary>Öffnet den Subnode, den ein call-Node über sein 'target' referenziert
    /// (z. B. per Doppelklick auf den Node). Gibt true zurück, wenn ein Subnode gefunden wurde.</summary>
    public bool OpenSubnodeByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var sub = Subnodes.FirstOrDefault(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (sub is null) return false;
        OpenTab(sub);
        return true;
    }

    /// <summary>Closes an open tab (the main tab cannot be closed).</summary>
    public void CloseTab(FlowTabViewModel tab)
    {
        if (!tab.CanClose) return;
        var wasActive = tab == _activeTab;
        OpenTabs.Remove(tab);
        if (wasActive)
        {
            var main = Tabs.FirstOrDefault(t => !t.IsSubFlow);
            SwitchTab(main ?? OpenTabs.FirstOrDefault());
        }
    }

    /// <summary>
    /// Finds the tab view model that contains the given node id.
    /// </summary>
    public FlowTabViewModel? FindTabOfNode(string nodeId) =>
        Tabs.FirstOrDefault(t => t.Nodes.Any(n => n.Id == nodeId));
}
