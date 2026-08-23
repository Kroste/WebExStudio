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
    // ── Wire operations ───────────────────────────────────────────────────────

    public bool AddWire(string sourceId, int outPort, string targetId, int inPort = 0)
    {
        if (Document is null || _activeTab is null) return false;
        if (sourceId == targetId) return false;

        var sourceNode = Document.GetNode(sourceId);
        if (sourceNode is null) return false;

        // Expand wires list to accommodate the port index
        while (sourceNode.Wires.Count <= outPort)
            sourceNode.Wires.Add([]);

        if (sourceNode.Wires[outPort].Contains(targetId)) return false;

        sourceNode.Wires[outPort].Add(targetId);
        Wires.Add(new WireViewModel(sourceId, outPort, targetId, inPort));
        MarkDirty();
        Log.Debug("Verbindung hinzugefügt: {0} → {1}", sourceId, targetId);
        return true;
    }

    public void RemoveWire(WireViewModel wire)
    {
        if (Document is null) return;
        var sourceNode = Document.GetNode(wire.SourceNodeId);
        if (sourceNode is not null && sourceNode.Wires.Count > wire.OutputPort)
            sourceNode.Wires[wire.OutputPort].Remove(wire.TargetNodeId);
        Wires.Remove(wire);
        MarkDirty();
        Log.Debug("Verbindung entfernt: {0} → {1}", wire.SourceNodeId, wire.TargetNodeId);
    }

    public void RefreshWires()
    {
        Wires.Clear();
        if (_activeTab is not null && Document is not null)
        {
            foreach (var nodeVm in _activeTab.Nodes)
            {
                var node = nodeVm.Model;
                for (int port = 0; port < node.Wires.Count; port++)
                {
                    foreach (var targetId in node.Wires[port])
                        Wires.Add(new WireViewModel(node.Id, port, targetId));
                }
            }
        }
        // Re-render connections after the collection is populated (mutating the
        // ObservableCollection alone doesn't re-trigger the view's RefreshConnections).
        this.RaisePropertyChanged(nameof(Wires));
    }

    // ── Node operations ───────────────────────────────────────────────────────

    public NodeViewModel AddNode(string type, double x, double y)
    {
        if (Document is null || _activeTab is null)
        {
            Document ??= FlowSerializer2.CreateEmpty();
            LoadDocument(Document);
        }

        var seqIndex = _activeTab!.Nodes.Count;
        var node = new FlowNode
        {
            Type = type,
            TabId = _activeTab.Id,
            X = x,
            Y = y,
            SeqIndex = seqIndex,
        };

        // Set default config values from NodeDefinition
        var def = NodeCatalog.Get(type);
        if (def is not null)
        {
            foreach (var prop in def.Properties.Where(p => p.DefaultValue is not null))
                node.Config[prop.Key] = prop.DefaultValue!;
        }

        Document.Nodes.Add(node);
        var vm = new NodeViewModel(node);
        _activeTab.Nodes.Add(vm);
        SelectNode(vm);
        MarkDirty();
        Log.Debug("Node hinzugefügt: {0} @ ({1:F0},{2:F0})", type, x, y);
        return vm;
    }

    /// <summary>
    /// Fügt unterhalb von <paramref name="anchor"/> einen neuen Node hinzu, übernimmt
    /// Config/Bezeichnung und verbindet ihn vom Ausgang 0 des Ankers. Für KI-Vorschläge.
    /// </summary>
    public NodeViewModel AddConnectedNode(NodeViewModel anchor, string type,
        IReadOnlyDictionary<string, string> config, string label)
    {
        PushUndo();
        var vm = AddNode(type, anchor.X, anchor.Y + 120);
        foreach (var kv in config)
            vm.Model.Config[kv.Key] = kv.Value;
        vm.Label = label;
        vm.RaiseTitleChanged();
        AddWire(anchor.Id, 0, vm.Id);
        this.RaisePropertyChanged(nameof(Wires)); // View → Verbindungen neu zeichnen
        return vm;
    }

    public void DeleteNode(NodeViewModel vm)
    {
        if (Document is null || _activeTab is null) return;
        Log.Debug("Node gelöscht: {0} ({1})", vm.Id, vm.ActionType);

        // Remove all wires to/from this node
        var wiresToRemove = Wires.Where(w => w.SourceNodeId == vm.Id || w.TargetNodeId == vm.Id).ToList();
        foreach (var wire in wiresToRemove)
            RemoveWire(wire);

        Document.Nodes.Remove(vm.Model);
        _activeTab.Nodes.Remove(vm);

        SelectedNodes.Remove(vm);
        if (SelectedNode == vm) SelectedNode = SelectedNodes.LastOrDefault();
        MarkDirty();
    }

    public NodeViewModel? FindNode(string id)
    {
        foreach (var tab in Tabs)
        {
            var vm = tab.Nodes.FirstOrDefault(n => n.Id == id);
            if (vm is not null) return vm;
        }
        return null;
    }
}
