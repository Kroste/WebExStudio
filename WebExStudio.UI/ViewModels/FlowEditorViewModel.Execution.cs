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
    // ── Execution state ───────────────────────────────────────────────────────

    public void SetActiveNode(string nodeId)
    {
        foreach (var tab in Tabs)
            foreach (var n in tab.Nodes)
            {
                n.IsActive = n.Id == nodeId;
                n.IsNext = false; // der laufende Node ist nicht mehr „der nächste"
            }

        // View follows execution: open/switch to the tab containing the active node.
        var owner = FindTabOfNode(nodeId);
        if (owner is not null && owner != ActiveTab)
            OpenTab(owner);
    }

    /// <summary>Markiert den im pausierten Zustand als Nächstes auszuführenden Node.</summary>
    public void SetNextNode(string nodeId)
    {
        foreach (var tab in Tabs)
            foreach (var n in tab.Nodes)
                n.IsNext = n.Id == nodeId;

        // Ansicht zum anstehenden Node führen (z. B. wenn er in einem Subnode liegt).
        var owner = FindTabOfNode(nodeId);
        if (owner is not null && owner != ActiveTab)
            OpenTab(owner);
    }

    /// <summary>Öffnet den Tab des Nodes und wählt ihn aus (für „zum Node springen").</summary>
    public NodeViewModel? FocusNode(string nodeId)
    {
        var tab = FindTabOfNode(nodeId);
        if (tab is not null) OpenTab(tab);
        var vm = FindNode(nodeId);
        if (vm is not null) SelectNode(vm);
        return vm;
    }

    public void SetNodeStatus(string nodeId, ExecutionStatusUi status)
    {
        var vm = FindNode(nodeId);
        if (vm is not null) vm.Status = status;
    }

    public void ClearExecutionState()
    {
        foreach (var tab in Tabs)
            foreach (var n in tab.Nodes)
            {
                n.IsActive = false;
                n.IsNext = false;
                n.Status = ExecutionStatusUi.None;
            }
    }

    public void MarkDirty()
    {
        IsDirty = true;
        this.RaisePropertyChanged(nameof(Title));
        ValidateAndMark();
    }

    /// <summary>Validiert den Flow und markiert fehlerhafte Nodes (Live-Validierung).</summary>
    public void ValidateAndMark()
    {
        if (Document is null) return;
        var byNode = WebExStudio.Core.Validation.FlowValidator.Validate(Document).Issues
            .Where(i => i.Severity == WebExStudio.Core.Validation.FlowIssueSeverity.Error && !string.IsNullOrEmpty(i.NodeId))
            .GroupBy(i => i.NodeId!)
            .ToDictionary(g => g.Key, g => string.Join("\n", g.Select(i => i.Message)));

        foreach (var tab in Tabs)
            foreach (var n in tab.Nodes)
                n.ValidationError = byNode.TryGetValue(n.Id, out var msg) ? msg : null;
    }
}
