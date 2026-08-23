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
        ValidateAndMark();
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
}
