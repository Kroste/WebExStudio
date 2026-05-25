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

    /// <summary>All tab view models (main, named subnodes, block-node branch tabs) — master list for lookups.</summary>
    public ObservableCollection<FlowTabViewModel> Tabs { get; } = [];

    /// <summary>Tabs currently shown in the tab bar (Main + opened subnodes/branches).</summary>
    public ObservableCollection<FlowTabViewModel> OpenTabs { get; } = [];

    /// <summary>All named, standalone subnodes — shown in the Subnodes list panel.</summary>
    public ObservableCollection<FlowTabViewModel> Subnodes { get; } = [];

    /// <summary>Names of all subnodes — used for the call-target dropdown.</summary>
    public IEnumerable<string> SubnodeNames => Subnodes.Select(s => s.Name!).Where(n => !string.IsNullOrEmpty(n));

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

    /// <summary>Groups (visual boxes) on the active tab.</summary>
    public ObservableCollection<GroupViewModel> Groups { get; } = [];

    /// <summary>All currently multi-selected nodes (the primary is also <see cref="SelectedNode"/>).</summary>
    public ObservableCollection<NodeViewModel> SelectedNodes { get; } = [];

    /// <summary>The primary selected node (drives the properties panel). Visual selection
    /// (IsSelected) is managed via the selection set in <see cref="SelectNode"/>.</summary>
    public NodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set => this.RaiseAndSetIfChanged(ref _selectedNode, value);
    }

    // ── Selection ──────────────────────────────────────────────────────────────

    public void ClearSelection()
    {
        foreach (var n in SelectedNodes) n.IsSelected = false;
        SelectedNodes.Clear();
        SelectedNode = null;
    }

    /// <summary>Selects a node. With <paramref name="additive"/> (Ctrl) it toggles membership.</summary>
    public void SelectNode(NodeViewModel? node, bool additive = false)
    {
        if (node is null) { if (!additive) ClearSelection(); return; }

        if (additive)
        {
            if (SelectedNodes.Contains(node)) { SelectedNodes.Remove(node); node.IsSelected = false; }
            else { SelectedNodes.Add(node); node.IsSelected = true; }
            SelectedNode = SelectedNodes.LastOrDefault();
        }
        else
        {
            if (!(SelectedNodes.Count == 1 && SelectedNodes[0] == node))
            {
                ClearSelection();
                SelectedNodes.Add(node);
                node.IsSelected = true;
            }
            SelectedNode = node;
        }
    }

    /// <summary>Selects all nodes on the active tab whose bounds intersect the (world) rectangle.</summary>
    public void SelectInRect(Avalonia.Rect rect)
    {
        if (_activeTab is null) return;
        ClearSelection();
        foreach (var n in _activeTab.Nodes)
        {
            var nb = new Avalonia.Rect(n.X, n.Y, n.Width, n.Height);
            if (rect.Intersects(nb))
            {
                SelectedNodes.Add(n);
                n.IsSelected = true;
            }
        }
        SelectedNode = SelectedNodes.LastOrDefault();
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

    /// <summary>Replaces the current document with a fresh, empty flow (Main tab only).</summary>
    public void NewDocument()
    {
        Log.Info("Neuer Flow");
        LoadDocument(FlowSerializer2.CreateEmpty());
    }

    public void LoadDocument(FlowDocument2 doc)
    {
        Log.Info("Lade Dokument: {0} Tabs, {1} Nodes", doc.Tabs.Count, doc.Nodes.Count);
        Document = doc;
        SelectedNode = null;

        Tabs.Clear();
        OpenTabs.Clear();
        Subnodes.Clear();
        foreach (var tab in doc.Tabs)
        {
            var tabVm = new FlowTabViewModel(tab);
            foreach (var node in doc.GetNodes(tab.Id))
                tabVm.Nodes.Add(new NodeViewModel(node));
            Tabs.Add(tabVm);
            if (tabVm.IsSubnode) Subnodes.Add(tabVm);
        }

        var mainTab = Tabs.FirstOrDefault(t => !t.IsSubFlow) ?? Tabs.FirstOrDefault();
        if (mainTab is not null) OpenTabs.Add(mainTab);
        _activeTab = null;
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

    // ── Execution state ───────────────────────────────────────────────────────

    public void SetActiveNode(string nodeId)
    {
        foreach (var tab in Tabs)
            foreach (var n in tab.Nodes)
                n.IsActive = n.Id == nodeId;

        // View follows execution: open/switch to the tab containing the active node.
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
                n.Status = ExecutionStatusUi.None;
            }
    }

    public void MarkDirty()
    {
        IsDirty = true;
        this.RaisePropertyChanged(nameof(Title));
    }
}
