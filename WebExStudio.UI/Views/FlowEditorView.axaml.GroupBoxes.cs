using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using NLog;
using WebExStudio.Core.Localization;
using WebExStudio.UI.Controls;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Views;

// Teil von FlowEditorView (Code-behind) — Aufteilung nach Verantwortlichkeit.
public partial class FlowEditorView
{
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
        var del = new MenuItem { Header = Loc.T("Menu_DeleteWire") };
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
}
