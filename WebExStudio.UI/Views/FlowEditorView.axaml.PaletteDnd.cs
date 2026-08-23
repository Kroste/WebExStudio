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
            name = def is not null ? Core.Models.NodeCatalog.LocalizedName(def) : type;
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
            var groupItem = new MenuItem { Header = Loc.T("Menu_Group") };
            groupItem.Click += (_, _) =>
            {
                Log.Info("Gruppieren: {0} Nodes", Vm.SelectedNodes.Count);
                Vm.PushUndo();
                Vm.CreateGroupFromSelection();
            };
            menu.Items.Add(groupItem);

            var subItem = new MenuItem { Header = Loc.T("Menu_SubFromSel") };
            subItem.Click += async (_, _) => await ExtractSelectionAsync();
            menu.Items.Add(subItem);

            menu.Items.Add(new Separator());
        }

        foreach (var category in Core.Models.NodeCatalog.Categories)
        {
            var header = new MenuItem { Header = Core.Models.NodeCatalog.LocalizedCategory(category) };
            foreach (var def in Core.Models.NodeCatalog.GetByCategory(category).Where(d => !d.Hidden))
            {
                var d = def;
                var item = new MenuItem { Header = $"{d.Icon}  {Core.Models.NodeCatalog.LocalizedName(d)}" };
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
        var dlg = new SubnodeDialog(Loc.T("SubDlg_FromSel"), "", "");
        await ShowDialogOverOwner(dlg);
        if (!dlg.Confirmed) return;
        var sub = Vm.ExtractSelectionToSubnode(dlg.SubnodeName, dlg.SubnodeLabel);
        if (sub is null)
            Log.Warn("Subnode aus Auswahl fehlgeschlagen (Name leer/vergeben): {0}", dlg.SubnodeName);
    }
}
