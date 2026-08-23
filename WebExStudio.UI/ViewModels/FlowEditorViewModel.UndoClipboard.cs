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
    // ── Undo / Redo (Snapshot des gesamten Dokuments) ───────────────────────────
    private const int UndoCap = 100;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Sichert den aktuellen Zustand für „Rückgängig" (vor einer Änderung aufrufen).</summary>
    public void PushUndo()
    {
        if (Document is null || _restoring) return;
        _undo.Add(FlowSerializer2.Serialize(Document));
        if (_undo.Count > UndoCap) _undo.RemoveAt(0);
        _redo.Clear();
        RaiseUndoRedo();
    }

    public void Undo()
    {
        if (_undo.Count == 0 || Document is null) return;
        _redo.Add(FlowSerializer2.Serialize(Document));
        RestoreSnapshot(Pop(_undo));
    }

    public void Redo()
    {
        if (_redo.Count == 0 || Document is null) return;
        _undo.Add(FlowSerializer2.Serialize(Document));
        RestoreSnapshot(Pop(_redo));
    }

    private static string Pop(List<string> stack)
    {
        var v = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return v;
    }

    private void RestoreSnapshot(string json)
    {
        var path = Document?.FilePath;
        var doc = FlowSerializer2.Deserialize(json);
        doc.FilePath = path;
        _restoring = true;
        LoadDocument(doc);
        _restoring = false;
        MarkDirty();
        RaiseUndoRedo();
    }

    private void RaiseUndoRedo()
    {
        this.RaisePropertyChanged(nameof(CanUndo));
        this.RaisePropertyChanged(nameof(CanRedo));
    }

    // ── Kopieren / Einfügen / Duplizieren ───────────────────────────────────────
    public bool HasClipboard => _clipboard.Count > 0;

    public void CopySelection()
    {
        _clipboard = SelectedNodes.Select(n => CloneModel(n.Model)).ToList();
        this.RaisePropertyChanged(nameof(HasClipboard));
    }

    public void PasteClipboard()
    {
        if (_clipboard.Count == 0) return;
        PushUndo();
        PasteNodes(_clipboard, 30, 30);
    }

    public void DuplicateSelection()
    {
        if (SelectedNodes.Count == 0) return;
        PushUndo();
        PasteNodes(SelectedNodes.Select(n => CloneModel(n.Model)).ToList(), 30, 30);
    }

    private static FlowNode CloneModel(FlowNode n) => new()
    {
        Id = n.Id, Type = n.Type, TabId = n.TabId, Label = n.Label,
        X = n.X, Y = n.Y, SeqIndex = n.SeqIndex,
        Config = new Dictionary<string, string>(n.Config),
        Wires = n.Wires.Select(p => p.ToList()).ToList(),
    };

    /// <summary>Fügt Kopien der Quell-Nodes (mit neuen IDs) in den aktiven Tab ein und wählt sie aus.
    /// Verbindungen innerhalb der Auswahl werden übernommen (auf die neuen IDs umgeschrieben).</summary>
    private void PasteNodes(List<FlowNode> source, double dx, double dy)
    {
        if (_activeTab is null || Document is null || source.Count == 0) return;
        var idMap = source.ToDictionary(n => n.Id, _ => NewId());
        var newVms = new List<NodeViewModel>();
        foreach (var s in source)
        {
            var node = new FlowNode
            {
                Id = idMap[s.Id], Type = s.Type, TabId = _activeTab.Id,
                Label = s.Label, X = s.X + dx, Y = s.Y + dy,
                SeqIndex = _activeTab.Nodes.Count,
                Config = new Dictionary<string, string>(s.Config),
                Wires = s.Wires.Select(port =>
                    port.Where(idMap.ContainsKey).Select(t => idMap[t]).ToList()).ToList(),
            };
            Document.Nodes.Add(node);
            var vm = new NodeViewModel(node);
            _activeTab.Nodes.Add(vm);
            newVms.Add(vm);
        }
        ClearSelection();
        foreach (var vm in newVms) { SelectedNodes.Add(vm); vm.IsSelected = true; }
        SelectedNode = newVms.LastOrDefault();
        RefreshWires();
        MarkDirty();
        Log.Debug("Eingefügt: {0} Node(s)", newVms.Count);
    }
}
