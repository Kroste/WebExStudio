using System.Collections.ObjectModel;
using NLog;
using ReactiveUI;
using WebExStudio.Core.Models;
using WebExStudio.Core.Serialization;

namespace WebExStudio.UI.ViewModels;

public sealed class FlowEditorViewModel : ViewModelBase
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private FlowDocument2? _document;
    private FlowTabViewModel? _activeTab;
    private NodeViewModel? _selectedNode;
    private bool _isDirty;

    public FlowDocument2? Document
    {
        get => _document;
        private set
        {
            this.RaiseAndSetIfChanged(ref _document, value);
            this.RaisePropertyChanged(nameof(CanSave));
        }
    }

    /// <summary>True when there's a document that can be saved.</summary>
    public bool CanSave => Document is not null;

    public ObservableCollection<FlowTabViewModel> Tabs { get; } = [];

    public FlowTabViewModel? ActiveTab
    {
        get => _activeTab;
        private set
        {
            this.RaiseAndSetIfChanged(ref _activeTab, value);
            this.RaisePropertyChanged(nameof(Nodes));
            RefreshWires();
        }
    }

    /// <summary>Nodes on the currently active tab.</summary>
    public ObservableCollection<NodeViewModel> Nodes =>
        _activeTab?.Nodes ?? _emptyNodes;
    private static readonly ObservableCollection<NodeViewModel> _emptyNodes = [];

    /// <summary>Wires visible on the current tab (populated only for main-canvas tabs).</summary>
    public ObservableCollection<WireViewModel> Wires { get; } = [];

    public NodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (_selectedNode != null) _selectedNode.IsSelected = false;
            this.RaiseAndSetIfChanged(ref _selectedNode, value);
            if (value != null) value.IsSelected = true;
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set => this.RaiseAndSetIfChanged(ref _isDirty, value);
    }

    public string Title => Document is null
        ? "Kein Flow geöffnet"
        : $"{(Document.FilePath is null ? "Unbenannt" : Path.GetFileNameWithoutExtension(Document.FilePath))}{(IsDirty ? " *" : "")}";

    // ── Load / Save ──────────────────────────────────────────────────────────

    public async Task LoadAsync(string path)
    {
        Log.Info("Öffne Flow-Datei: {0}", path);
        var doc = await FlowSerializer2.LoadAsync(path);
        LoadDocument(doc);
    }

    public void LoadDocument(FlowDocument2 doc)
    {
        Log.Info("Lade Dokument: {0} Tabs, {1} Nodes", doc.Tabs.Count, doc.Nodes.Count);
        Document = doc;
        SelectedNode = null;

        Tabs.Clear();
        foreach (var tab in doc.Tabs)
        {
            var tabVm = new FlowTabViewModel(tab);
            foreach (var node in doc.GetNodes(tab.Id))
                tabVm.Nodes.Add(new NodeViewModel(node));
            Tabs.Add(tabVm);
        }

        var mainTab = Tabs.FirstOrDefault(t => !t.IsSubFlow) ?? Tabs.FirstOrDefault();
        SwitchTab(mainTab);
        IsDirty = false;
        this.RaisePropertyChanged(nameof(Title));
    }

    public async Task SaveAsync(string path)
    {
        if (Document is null) return;
        Log.Info("Speichere Dokument: {0}", path);

        // Sync VM positions back to model
        foreach (var tab in Tabs)
            foreach (var nodeVm in tab.Nodes)
            {
                nodeVm.Model.X = nodeVm.X;
                nodeVm.Model.Y = nodeVm.Y;
            }

        await FlowSerializer2.SaveAsync(Document, path);
        IsDirty = false;
        this.RaisePropertyChanged(nameof(Title));
    }

    // ── Tab operations ────────────────────────────────────────────────────────

    public void SwitchTab(FlowTabViewModel? tab)
    {
        if (tab is null || tab == _activeTab) return;
        Log.Debug("Tab wechseln: {0}", tab.Label);
        _activeTab = tab;
        this.RaisePropertyChanged(nameof(ActiveTab));
        this.RaisePropertyChanged(nameof(Nodes));
        RefreshWires();
    }

    /// <summary>
    /// Navigates into a block node's sub-flow tab for the given slot ("then","else","body").
    /// Creates the tab if it doesn't exist yet.
    /// </summary>
    public void OpenSubTab(NodeViewModel blockNode, string slot)
    {
        if (Document is null) return;

        var configKey = slot switch
        {
            "then" => "thenTabId",
            "else" => "elseTabId",
            _ => "bodyTabId",
        };

        string? tabId = blockNode.Model.Config.TryGetValue(configKey, out var v) ? v : null;
        FlowTabViewModel? tabVm = tabId is null ? null : Tabs.FirstOrDefault(t => t.Id == tabId);

        if (tabVm is null)
        {
            // Create a new sub-flow tab
            var label = slot switch
            {
                "then" => "Then",
                "else" => "Else",
                _ => "Body",
            };
            var newTab = new FlowTab
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Label = $"{blockNode.DisplayName}: {label}",
                IsSubFlow = true,
                OwnerNodeId = blockNode.Id,
                Slot = slot,
            };
            Document.Tabs.Add(newTab);
            blockNode.Model.Config[configKey] = newTab.Id;
            tabVm = new FlowTabViewModel(newTab);
            Tabs.Add(tabVm);
            MarkDirty();
        }

        SwitchTab(tabVm);
    }

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
        if (_activeTab is null || _activeTab.IsSubFlow || Document is null) return;

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
        SelectedNode = vm;
        MarkDirty();
        Log.Debug("Node hinzugefügt: {0} @ ({1:F0},{2:F0})", type, x, y);
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

        // Also remove sub-flow tabs owned by this node
        if (vm.HasSubFlows)
        {
            var ownedTabs = Document.Tabs.Where(t => t.OwnerNodeId == vm.Id).ToList();
            foreach (var tab in ownedTabs)
            {
                Document.Tabs.Remove(tab);
                var tabVm = Tabs.FirstOrDefault(t => t.Id == tab.Id);
                if (tabVm is not null) Tabs.Remove(tabVm);
                // Remove nodes belonging to this sub-tab
                var subNodes = Document.Nodes.Where(n => n.TabId == tab.Id).ToList();
                foreach (var sn in subNodes) Document.Nodes.Remove(sn);
            }
        }

        if (SelectedNode == vm) SelectedNode = null;
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

    // ── Execution state ───────────────────────────────────────────────────────

    public void SetActiveNode(string nodeId)
    {
        foreach (var tab in Tabs)
            foreach (var n in tab.Nodes)
                n.IsActive = n.Id == nodeId;
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
                n.Status = ExecutionStatusUi.None;
            }
    }

    public void MarkDirty()
    {
        IsDirty = true;
        this.RaisePropertyChanged(nameof(Title));
    }
}
