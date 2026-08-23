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
        PreviewDefinition = null; // echte Auswahl hebt die Palette-Vorschau auf

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

    /// <summary>Standard-Rastergröße (passend zum Hintergrund-Punktraster).</summary>
    public const double GridSize = 20.0;

    /// <summary>Verschiebt alle ausgewählten Nodes um (dx, dy) — z. B. per Pfeiltasten.</summary>
    public void MoveSelectedBy(double dx, double dy)
    {
        if (SelectedNodes.Count == 0) return;
        foreach (var n in SelectedNodes) { n.X += dx; n.Y += dy; }
        MarkDirty();
    }

    /// <summary>Richtet alle Nodes des aktiven Tabs am Raster aus (rundet X/Y auf Vielfache).</summary>
    public void SnapAllToGrid(double grid = GridSize)
    {
        if (_activeTab is null || grid <= 0) return;
        PushUndo();
        foreach (var n in _activeTab.Nodes)
        {
            n.X = System.Math.Round(n.X / grid) * grid;
            n.Y = System.Math.Round(n.Y / grid) * grid;
        }
        MarkDirty();
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
        ? Loc.T("VM_NoFlowOpen")
        : $"{(Document.FilePath is null ? Loc.T("VM_Untitled") : Path.GetFileNameWithoutExtension(Document.FilePath))}{(IsDirty ? " *" : "")}";
}
