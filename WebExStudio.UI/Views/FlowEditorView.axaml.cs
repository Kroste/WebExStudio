using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using NLog;
using WebExStudio.UI.Controls;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Views;

public partial class FlowEditorView : UserControl
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private FlowEditorViewModel? Vm => DataContext as FlowEditorViewModel;

    /// <summary>Doppelklick auf einen Tresor-Node → Anmeldedaten-Verwaltung öffnen.</summary>
    public event EventHandler? CredentialVaultRequested;

    private readonly Dictionary<string, NodeControl> _nodeControls = [];
    private readonly Dictionary<string, GroupControl> _groupControls = [];
    private WireViewModel? _selectedWire;

    public FlowEditorView()
    {
        InitializeComponent();
        Focusable = true;
        Canvas.GridOverlay = GridOverlay;
        Canvas.ConnectionRenderer = ConnectionRenderer;
        ConnectionRenderer.RenderTransform = Canvas.WorldTransform;
        // Gleicher Transform-Ursprung wie der Canvas, sonst driften Wires nach einem Resize.
        ConnectionRenderer.RenderTransformOrigin = Avalonia.RelativePoint.TopLeft;
        Canvas.WireDropped += OnWireDropped;
        Canvas.SelectionCompleted += OnSelectionCompleted;
        DataContextChanged += OnDataContextChanged;
        PointerPressed += OnCanvasPointerPressed;

        // Drop-Ziel ist die untransformierte Viewport-Fläche (CanvasArea), NICHT der gezoomte/
        // gescrollte NodeCanvas: dessen Trefferfläche ist nach Zoom/Scroll nur ein verschobenes
        // Band. Außerhalb davon kam Drag&Drop aus der Palette nicht mehr an (Ghost verschwand,
        // kein Drop). CanvasArea deckt immer das ganze Sichtfeld ab; die Drop-Position wird
        // ohnehin relativ zu CanvasArea gemessen und per CanvasToWorld umgerechnet.
        DragDrop.SetAllowDrop(CanvasArea, true);
        CanvasArea.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        CanvasArea.AddHandler(DragDrop.DropEvent, OnDrop);
        CanvasArea.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
    }

    /// <summary>Mausrad auf der untransformierten Viewport-Fläche → an den Canvas weiterreichen.</summary>
    private void OnCanvasAreaWheel(object? sender, PointerWheelEventArgs e)
    {
        Canvas.ApplyWheel(e.Delta, e.GetPosition(CanvasArea), e.KeyModifiers);
        e.Handled = true;
    }

    private void SelectWire(WireViewModel? wire)
    {
        _selectedWire = wire;
        ConnectionRenderer.SelectedWire = wire;
    }

    public void ResetView() => Canvas.ResetView();

    /// <summary>Öffnet den Tab des Nodes, wählt ihn aus und zentriert die Ansicht darauf.</summary>
    public void FocusNode(string nodeId)
    {
        if (Vm?.FocusNode(nodeId) is not { } node) return;
        Canvas.CenterOn(new Point(node.X + node.Width / 2, node.Y + node.Height / 2));
    }

    public void FitToView()
    {
        var bounds = _nodeControls.Values
            .Select(ctrl => new Rect(ctrl.ViewModel.X, ctrl.ViewModel.Y,
                                     ctrl.ViewModel.Width, ctrl.ViewModel.Height));
        Canvas.FitToView(bounds);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (Vm is null) return;
        Log.Debug("DataContext geändert");
        Vm.PropertyChanged += OnVmPropertyChanged;
        RebuildCanvas();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FlowEditorViewModel.ActiveTab) or nameof(FlowEditorViewModel.Nodes))
            RebuildCanvas();
        else if (e.PropertyName == nameof(FlowEditorViewModel.Wires))
            RefreshConnections();
        else if (e.PropertyName == nameof(FlowEditorViewModel.Groups))
            RebuildGroups();
    }

    private void OnSelectionCompleted(object? sender, Rect worldRect) => Vm?.SelectInRect(worldRect);

    // The Nodes collection we're currently observing for add/remove (palette + right-click).
    private System.Collections.Specialized.INotifyCollectionChanged? _observedNodes;

    private void RebuildCanvas()
    {
        // Stop observing the previous tab's collection
        if (_observedNodes is not null)
            _observedNodes.CollectionChanged -= OnNodesCollectionChanged;
        _observedNodes = null;

        SelectWire(null);
        Canvas.Children.Clear();
        _nodeControls.Clear();
        _groupControls.Clear();

        if (Vm?.ActiveTab is null) return;

        Log.Debug("RebuildCanvas: Tab={0}, {1} Nodes", Vm.ActiveTab.Label, Vm.ActiveTab.Nodes.Count);

        foreach (var nodeVm in Vm.ActiveTab.Nodes)
            AddNodeControl(nodeVm);

        RebuildGroups();
        RefreshConnections();
        UpdateTabButtonStyles();

        // Observe the active tab's node collection so additions from any source render
        _observedNodes = Vm.ActiveTab.Nodes;
        _observedNodes.CollectionChanged += OnNodesCollectionChanged;
    }

    private void OnNodesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case System.Collections.Specialized.NotifyCollectionChangedAction.Add when e.NewItems is not null:
                foreach (NodeViewModel vm in e.NewItems)
                    if (!_nodeControls.ContainsKey(vm.Id))
                        AddNodeControl(vm);
                break;
            case System.Collections.Specialized.NotifyCollectionChangedAction.Remove when e.OldItems is not null:
                foreach (NodeViewModel vm in e.OldItems)
                    if (_nodeControls.Remove(vm.Id, out var ctrl))
                        Canvas.Children.Remove(ctrl);
                break;
            default:
                // Reset / Replace / Move — rebuild controls without touching the subscription
                Canvas.Children.Clear();
                _nodeControls.Clear();
                if (Vm?.ActiveTab is not null)
                    foreach (var vm in Vm.ActiveTab.Nodes)
                        AddNodeControl(vm);
                break;
        }
        RefreshConnections();
    }

    private void AddNodeControl(NodeViewModel nodeVm)
    {
        var ctrl = new NodeControl(nodeVm);
        ctrl.PointerPressed += OnNodePointerPressed;
        ctrl.DeleteRequested += OnNodeDeleteRequested;
        _nodeControls[nodeVm.Id] = ctrl;
        Canvas.Children.Add(ctrl);
        Avalonia.Controls.Canvas.SetLeft(ctrl, nodeVm.X);
        Avalonia.Controls.Canvas.SetTop(ctrl, nodeVm.Y);

        nodeVm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(NodeViewModel.X) or nameof(NodeViewModel.Y))
            {
                if (_nodeControls.TryGetValue(nodeVm.Id, out var currentCtrl))
                {
                    Avalonia.Controls.Canvas.SetLeft(currentCtrl, nodeVm.X);
                    Avalonia.Controls.Canvas.SetTop(currentCtrl, nodeVm.Y);
                }
                RefreshConnections();
                RepositionGroups();
            }
        };
    }

    private void OnNodePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not NodeControl ctrl || Vm is null) return;
        Log.Debug("Node ausgewählt: {0} ({1})", ctrl.ViewModel.Id, ctrl.ViewModel.ActionType);

        // Doppelklick auf den Tresor-Node öffnet die Anmeldedaten-Verwaltung.
        if (e.ClickCount == 2 && ctrl.ViewModel.ActionType == "credential_store")
        {
            CredentialVaultRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        // Doppelklick auf einen call-Node öffnet den referenzierten Subnode. Direkt hier
        // prüfen (statt über DoubleTapped), weil der Node-Drag den PointerPressed als Handled
        // markiert und so die Tap-Gesten-Erkennung unterdrückt.
        if (e.ClickCount == 2 && ctrl.ViewModel.ActionType == "call")
        {
            var target = ctrl.ViewModel.Model.Get("target");
            if (Vm.OpenSubnodeByName(target))
            {
                Log.Info("Subnode per Doppelklick öffnen: {0}", target);
                e.Handled = true;
                return;
            }
            Log.Warn("Doppelklick auf call-Node {0}: Subnode '{1}' nicht gefunden", ctrl.ViewModel.Id, target);
        }

        var ctrlKey = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (ctrlKey)
            Vm.SelectNode(ctrl.ViewModel, additive: true);
        else if (!Vm.SelectedNodes.Contains(ctrl.ViewModel))
            Vm.SelectNode(ctrl.ViewModel); // keep existing multi-selection when dragging a member
        else
            Vm.SelectedNode = ctrl.ViewModel; // primary follows the pressed node

        SelectWire(null);
        Focus();

        var pos = e.GetPosition(ctrl);
        var outPort = ctrl.OutputPortAt(pos);

        // Wire drag: pointer on a specific output port
        if (e.GetCurrentPoint(Canvas).Properties.IsLeftButtonPressed && outPort >= 0)
        {
            Canvas.BeginWireDrag(ctrl.ViewModel, outPort, e);
            e.Handled = true;
            return;
        }

        // Node drag: left button, not on port, not Alt — move the whole selection together.
        if (e.GetCurrentPoint(Canvas).Properties.IsLeftButtonPressed
            && !e.KeyModifiers.HasFlag(KeyModifiers.Alt)
            && outPort < 0
            && !ctrl.IsOnInputPort(pos))
        {
            Canvas.BeginNodeDrag(ctrl.ViewModel, Vm.SelectedNodes.ToList(), e.GetPosition(CanvasArea), e);
            e.Handled = true;
        }
    }

    private void OnWireDropped(object? sender, WireDropEventArgs e)
    {
        if (Vm is null) return;

        // Find node at drop position
        NodeControl? targetCtrl = null;
        foreach (var ctrl in _nodeControls.Values)
        {
            var nodePos = new Point(ctrl.ViewModel.X, ctrl.ViewModel.Y);
            var localPos = new Point(e.WorldPos.X - nodePos.X, e.WorldPos.Y - nodePos.Y);
            if (localPos.X >= -8 && localPos.X <= ctrl.ViewModel.Width + 8 &&
                localPos.Y >= -8 && localPos.Y <= ctrl.ViewModel.Height + 8)
            {
                if (ctrl.IsOnInputPort(localPos))
                {
                    targetCtrl = ctrl;
                    break;
                }
            }
        }

        if (targetCtrl is null) return;
        if (targetCtrl.ViewModel.Id == e.Source.Id) return;

        Log.Debug("Wire dropped: {0} → {1}", e.Source.Id, targetCtrl.ViewModel.Id);
        Vm.PushUndo();
        Vm.AddWire(e.Source.Id, e.OutputPort, targetCtrl.ViewModel.Id);
        RefreshConnections();
    }

    private void OnNodeDeleteRequested(object? sender, NodeViewModel vm)
    {
        Log.Info("Node löschen: {0} ({1})", vm.Id, vm.ActionType);
        Vm?.PushUndo();
        Vm?.DeleteNode(vm);
        RebuildCanvas();
    }

    /// <summary>Öffnet die Node-Suche (z. B. vom Toolbar-Button).</summary>
    public void OpenSearch() => ShowSearch();

    /// <summary>Öffnet die Node-Suche (Strg+F) und springt zum gewählten Node.</summary>
    private async void ShowSearch()
    {
        if (Vm is null) return;
        var dlg = new NodeSearchDialog(Vm);
        if (TopLevel.GetTopLevel(this) is Window owner) await dlg.ShowDialog(owner);
        else dlg.Show();
        if (dlg.SelectedNodeId is { } id) FocusNode(id);
    }

    private void RefreshConnections()
    {
        if (Vm is null) return;
        ConnectionRenderer.Update(Vm.Wires, _nodeControls.Values.Select(c => c.ViewModel));
        ConnectionRenderer.InvalidateVisual();
    }

    // ── Group boxes ────────────────────────────────────────────────────────────

    private void RebuildGroups()
    {
        foreach (var ctrl in _groupControls.Values)
            Canvas.Children.Remove(ctrl);
        _groupControls.Clear();

        if (Vm is null) return;
        foreach (var g in Vm.Groups)
        {
            var ctrl = new GroupControl(g);
            ctrl.MenuRequested += (_, group) => ShowGroupMenu(group);
            ctrl.RenameRequested += async (_, group) => await RenameGroupAsync(group);
            ctrl.MoveStarted += (_, t) =>
            {
                var members = MembersOf(t.Group);
                if (members.Count > 0)
                    Canvas.BeginNodeDrag(members[0], members, t.Args.GetPosition(CanvasArea), t.Args);
            };
            _groupControls[g.Id] = ctrl;
            Canvas.Children.Add(ctrl);
        }
        RepositionGroups();
    }

    private void RepositionGroups()
    {
        if (Vm is null || _groupControls.Count == 0) return;
        var map = _nodeControls.Values.ToDictionary(c => c.ViewModel.Id, c => c.ViewModel);
        foreach (var (id, ctrl) in _groupControls)
        {
            var b = ctrl.Group.Bounds(map);
            if (b == default) { ctrl.IsVisible = false; continue; }
            ctrl.IsVisible = true;
            // Pad sides; extend the top to host the header strip above the nodes.
            ctrl.SetBounds(new Rect(b.X - 14, b.Y - 30, b.Width + 28, b.Height + 44));
        }
    }

    private List<NodeViewModel> MembersOf(GroupViewModel group) =>
        group.NodeIds
            .Select(nid => _nodeControls.TryGetValue(nid, out var c) ? c.ViewModel : null)
            .Where(n => n is not null).Select(n => n!).ToList();

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Vm is null) return;

        // Ignore clicks that originated on a node (handled in OnNodePointerPressed)
        var elem = e.Source as Avalonia.StyledElement;
        while (elem is not null)
        {
            if (elem is NodeControl) return;
            elem = elem.Parent;
        }

        Focus();
        var props = e.GetCurrentPoint(this).Properties;
        // Position relativ zur untransformierten Viewport-Fläche messen (siehe NodeCanvas.ViewportPos).
        var canvasPos = e.GetPosition(CanvasArea);
        var hitWire = ConnectionRenderer.HitTest(Canvas.CanvasToWorld(canvasPos));

        if (props.IsRightButtonPressed)
        {
            if (hitWire is not null)
                ShowWireMenu(hitWire);
            else
                ShowAddNodeMenu(canvasPos);
            e.Handled = true;
        }
        else if (props.IsLeftButtonPressed)
        {
            SelectWire(hitWire);
            if (hitWire is not null) e.Handled = true;
        }
    }

    private void ShowWireMenu(WireViewModel wire)
    {
        SelectWire(wire);
        var menu = new ContextMenu();
        var del = new MenuItem { Header = "🗑 Verbindung löschen" };
        del.Click += (_, _) =>
        {
            Log.Info("Verbindung löschen: {0} → {1}", wire.SourceNodeId, wire.TargetNodeId);
            Vm?.PushUndo();
            Vm?.RemoveWire(wire);
            SelectWire(null);
            RefreshConnections();
        };
        menu.Items.Add(del);
        menu.Open(this);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Tastenkürzel mit Strg: Undo/Redo, Kopieren/Einfügen/Duplizieren, Suchen.
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && Vm is not null)
        {
            switch (e.Key)
            {
                case Key.Z: Vm.Undo(); e.Handled = true; return;
                case Key.Y: Vm.Redo(); e.Handled = true; return;
                case Key.C: Vm.CopySelection(); e.Handled = true; return;
                case Key.V: Vm.PasteClipboard(); e.Handled = true; return;
                case Key.D: Vm.DuplicateSelection(); e.Handled = true; return;
                case Key.F: ShowSearch(); e.Handled = true; return;
            }
        }

        // Pfeiltasten verschieben die Auswahl (Umschalt = fein, sonst ein Rasterschritt).
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down && Vm?.SelectedNodes.Count > 0)
        {
            var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 1.0 : FlowEditorViewModel.GridSize;
            var (dx, dy) = e.Key switch
            {
                Key.Left => (-step, 0.0),
                Key.Right => (step, 0.0),
                Key.Up => (0.0, -step),
                _ => (0.0, step),
            };
            Vm.PushUndo();
            Vm.MoveSelectedBy(dx, dy);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Delete or Key.Back)
        {
            if (_selectedWire is not null)
            {
                Log.Info("Verbindung löschen (Taste): {0} → {1}", _selectedWire.SourceNodeId, _selectedWire.TargetNodeId);
                Vm?.PushUndo();
                Vm?.RemoveWire(_selectedWire);
                SelectWire(null);
                RefreshConnections();
                e.Handled = true;
            }
            else if (Vm?.SelectedNode is { } node)
            {
                Log.Info("Node löschen (Taste): {0} ({1})", node.Id, node.ActionType);
                Vm.PushUndo();
                Vm.DeleteNode(node);
                RebuildCanvas();
                e.Handled = true;
            }
        }
        base.OnKeyDown(e);
    }

    // ── Palette drag-and-drop ─────────────────────────────────────────────────

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var ok = e.DataTransfer.Contains(NodePaletteView.NodeTypeFormat)
              || e.DataTransfer.Contains(SubnodePanelView.SubnodeNameFormat);
        e.DragEffects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        if (ok) UpdateDragGhost(e);
        else DragGhost.IsVisible = false;
    }

    private void OnDragLeave(object? sender, DragEventArgs e) => DragGhost.IsVisible = false;

    /// <summary>Zeigt eine Vorschau (Icon + Name) des gezogenen Nodes am Cursor.</summary>
    private void UpdateDragGhost(DragEventArgs e)
    {
        string? icon = null, name = null, color = null;
        if (e.DataTransfer.TryGetValue(NodePaletteView.NodeTypeFormat) is string type && !string.IsNullOrEmpty(type))
        {
            var def = Core.Models.NodeCatalog.Get(type);
            icon = def?.Icon ?? "⬚";
            name = def?.DisplayName ?? type;
            color = def?.Color ?? "#607D8B";
        }
        else if (e.DataTransfer.TryGetValue(SubnodePanelView.SubnodeNameFormat) is string sub && !string.IsNullOrEmpty(sub))
        {
            icon = "📞";
            name = sub;
            color = "#F57F17";
        }
        if (name is null) { DragGhost.IsVisible = false; return; }

        GhostIcon.Text = icon;
        GhostName.Text = name;
        var c = Avalonia.Media.Color.Parse(color!);
        DragGhost.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(60, c.R, c.G, c.B));
        DragGhost.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(150, c.R, c.G, c.B));
        var p = e.GetPosition(CanvasArea);
        DragGhost.RenderTransform = new Avalonia.Media.TranslateTransform(p.X + 8, p.Y + 8);
        DragGhost.IsVisible = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        DragGhost.IsVisible = false;
        if (Vm is null) return;
        var world = Canvas.CanvasToWorld(e.GetPosition(CanvasArea));

        // Subnode dragged from the list → create a call node targeting it.
        if (e.DataTransfer.TryGetValue(SubnodePanelView.SubnodeNameFormat) is string subnode && !string.IsNullOrEmpty(subnode))
        {
            Log.Info("Subnode per Drag&Drop einfügen: {0}", subnode);
            Vm.PushUndo();
            var vm = Vm.AddNode("call", world.X - 100, world.Y - 30);
            vm.Model.Config["target"] = subnode;
            vm.RaiseTitleChanged();
            return;
        }

        if (e.DataTransfer.TryGetValue(NodePaletteView.NodeTypeFormat) is not string type || string.IsNullOrEmpty(type)) return;
        Log.Info("Node per Drag&Drop hinzufügen: {0} @ ({1:F0},{2:F0})", type, world.X, world.Y);
        Vm.PushUndo();
        Vm.AddNode(type, world.X - 100, world.Y - 30); // center node on cursor
    }

    private void ShowAddNodeMenu(Point canvasPos)
    {
        var worldPos = Canvas.CanvasToWorld(canvasPos);
        Log.Debug("Kontext-Menü @ ({0:F0},{1:F0})", worldPos.X, worldPos.Y);
        var menu = new ContextMenu();

        // Aktionen für die aktuelle Mehrfachauswahl.
        if (Vm is { SelectedNodes.Count: >= 2 })
        {
            var groupItem = new MenuItem { Header = "📦  Gruppieren" };
            groupItem.Click += (_, _) =>
            {
                Log.Info("Gruppieren: {0} Nodes", Vm.SelectedNodes.Count);
                Vm.PushUndo();
                Vm.CreateGroupFromSelection();
            };
            menu.Items.Add(groupItem);

            var subItem = new MenuItem { Header = "📦  Subnode aus Auswahl" };
            subItem.Click += async (_, _) => await ExtractSelectionAsync();
            menu.Items.Add(subItem);

            menu.Items.Add(new Separator());
        }

        foreach (var category in Core.Models.NodeCatalog.Categories)
        {
            var header = new MenuItem { Header = category };
            foreach (var def in Core.Models.NodeCatalog.GetByCategory(category).Where(d => !d.Hidden))
            {
                var d = def;
                var item = new MenuItem { Header = $"{d.Icon}  {d.DisplayName}" };
                item.Click += (_, _) =>
                {
                    Log.Info("Node hinzufügen: {0} @ ({1:F0},{2:F0})", d.Type, worldPos.X, worldPos.Y);
                    Vm?.PushUndo();
                    // AddNode raises CollectionChanged → OnNodesCollectionChanged renders the control.
                    Vm?.AddNode(d.Type, worldPos.X, worldPos.Y);
                };
                header.Items.Add(item);
            }
            menu.Items.Add(header);
        }

        menu.Open(this);
    }

    /// <summary>Fragt Name/Bezeichnung ab und macht aus der Auswahl direkt einen Subnode.</summary>
    private async Task ExtractSelectionAsync()
    {
        if (Vm is null || Vm.SelectedNodes.Count == 0) return;
        var dlg = new SubnodeDialog("Subnode aus Auswahl", "", "");
        await ShowDialogOverOwner(dlg);
        if (!dlg.Confirmed) return;
        var sub = Vm.ExtractSelectionToSubnode(dlg.SubnodeName, dlg.SubnodeLabel);
        if (sub is null)
            Log.Warn("Subnode aus Auswahl fehlgeschlagen (Name leer/vergeben): {0}", dlg.SubnodeName);
    }

    // ── Group context menu ─────────────────────────────────────────────────────

    private void ShowGroupMenu(GroupViewModel group)
    {
        if (Vm is null) return;
        var menu = new ContextMenu();

        var extract = new MenuItem { Header = "📦  Subnode einrichten" };
        extract.Click += async (_, _) => await ExtractGroupAsync(group);

        var rename = new MenuItem { Header = "✎  Umbenennen" };
        rename.Click += async (_, _) => await RenameGroupAsync(group);

        var ungroup = new MenuItem { Header = "✖  Gruppe lösen" };
        ungroup.Click += (_, _) => Vm.Ungroup(group);

        menu.Items.Add(extract);
        menu.Items.Add(rename);
        menu.Items.Add(new Separator());
        menu.Items.Add(ungroup);
        menu.Open(this);
    }

    private async Task ExtractGroupAsync(GroupViewModel group)
    {
        if (Vm is null) return;
        var dlg = new SubnodeDialog("Subnode aus Gruppe", "", group.Label);
        await ShowDialogOverOwner(dlg);
        if (!dlg.Confirmed) return;

        var sub = Vm.ExtractGroupToSubnode(group, dlg.SubnodeName, dlg.SubnodeLabel);
        if (sub is null)
            Log.Warn("Subnode konnte nicht erstellt werden (Name leer oder vergeben): {0}", dlg.SubnodeName);
        else
            Log.Info("Gruppe '{0}' → Subnode '{1}'", group.Label, dlg.SubnodeName);
    }

    private async Task RenameGroupAsync(GroupViewModel group)
    {
        if (Vm is null) return;
        var dlg = new SubnodeDialog("Gruppe umbenennen", group.Label, "");
        await ShowDialogOverOwner(dlg);
        if (!dlg.Confirmed) return;
        Vm.RenameGroup(group, dlg.SubnodeName);
        if (_groupControls.TryGetValue(group.Id, out var ctrl)) ctrl.RefreshLabel();
    }

    private async Task ShowDialogOverOwner(Window dlg)
    {
        if (TopLevel.GetTopLevel(this) is Window owner)
            await dlg.ShowDialog(owner);
        else
            dlg.Show();
    }

    private void UpdateTabButtonStyles() { /* active-tab highlight handled by binding */ }

    // ── Tab bar interactions ──────────────────────────────────────────────────

    private void OnTabClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Control { Tag: FlowTabViewModel tab })
            Vm?.SwitchTab(tab);
    }

    private void OnTabClose(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Control { Tag: FlowTabViewModel tab })
            Vm?.CloseTab(tab);
    }
}
