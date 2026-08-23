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
    // ── Auto-Layout ─────────────────────────────────────────────────────────────
    /// <summary>Ordnet die Nodes des aktiven Tabs als von oben nach unten verlaufenden Graphen an.</summary>
    public void AutoLayoutActiveTab()
    {
        if (_activeTab is null || Document is null || _activeTab.Nodes.Count == 0) return;
        PushUndo();

        var nodes = _activeTab.Nodes.ToList();
        var ids = nodes.Select(n => n.Id).ToHashSet();
        var outAdj = nodes.ToDictionary(n => n.Id, _ => new List<string>());
        var indeg = nodes.ToDictionary(n => n.Id, _ => 0);
        foreach (var n in nodes)
            foreach (var port in Document.GetNode(n.Id)?.Wires ?? [])
                foreach (var t in port)
                    if (ids.Contains(t)) { outAdj[n.Id].Add(t); indeg[t]++; }

        // Längster Pfad (Tiefe) als vertikale Ebene; Kahn-Reihenfolge.
        var depth = nodes.ToDictionary(n => n.Id, _ => 0);
        var work = new Dictionary<string, int>(indeg);
        var q = new Queue<string>(nodes.Where(n => work[n.Id] == 0).Select(n => n.Id));
        while (q.Count > 0)
        {
            var u = q.Dequeue();
            foreach (var v in outAdj[u])
            {
                if (depth[v] < depth[u] + 1) depth[v] = depth[u] + 1;
                if (--work[v] == 0) q.Enqueue(v);
            }
        }

        const double colW = 240, rowH = 150, x0 = 80, y0 = 60;
        foreach (var level in nodes.GroupBy(n => depth[n.Id]).OrderBy(g => g.Key))
        {
            int col = 0;
            foreach (var n in level.OrderBy(n => n.Y).ThenBy(n => n.X))
            {
                n.Y = y0 + level.Key * rowH;
                n.X = x0 + col * colW;
                col++;
            }
        }
        MarkDirty();
        Log.Info("Auto-Layout: {0} Nodes angeordnet", nodes.Count);
    }

    /// <summary>Erzeugt aus der aktuellen Mehrfachauswahl direkt einen Subnode (gruppiert + extrahiert).</summary>
    public FlowTabViewModel? ExtractSelectionToSubnode(string name, string label)
    {
        if (SelectedNodes.Count == 0) return null;
        PushUndo();
        var g = CreateGroupFromSelection(label);
        return g is null ? null : ExtractGroupToSubnode(g, name, label);
    }
}
