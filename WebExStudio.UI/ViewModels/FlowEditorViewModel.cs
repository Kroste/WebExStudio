using System.Collections.ObjectModel;
using NLog;
using ReactiveUI;
using WebExStudio.Core.Localization;
using WebExStudio.Core.Models;
using WebExStudio.Core.Serialization;

namespace WebExStudio.UI.ViewModels;

public sealed partial class FlowEditorViewModel : ViewModelBase
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private FlowDocument2? _document;
    private FlowTabViewModel? _activeTab;
    private NodeViewModel? _selectedNode;
    private NodeDefinition? _previewDefinition;
    private bool _isDirty;

    private readonly List<string> _undo = [];
    private readonly List<string> _redo = [];
    private bool _restoring;
    private List<FlowNode> _clipboard = [];

    public FlowEditorViewModel()
    {
        // Tab-Titel („Kein Flow geöffnet" / „Unbenannt") bei Sprachwechsel aktualisieren.
        Loc.Instance.PropertyChanged += (_, _) => this.RaisePropertyChanged(nameof(Title));
    }

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

    /// <summary>Definition eines in der Palette angeklickten Nodes — das Eigenschaften-Panel zeigt
    /// dafür eine (nicht editierbare) Vorschau mit Hinweisen/Beispiel. Wird beim Auswählen eines
    /// echten Nodes geleert.</summary>
    public NodeDefinition? PreviewDefinition
    {
        get => _previewDefinition;
        set => this.RaiseAndSetIfChanged(ref _previewDefinition, value);
    }

    /// <summary>Liefert die im Tresor verfügbaren Secret-Platzhalter (von der UI verdrahtet);
    /// leer/null wenn gesperrt. Für den Secret-Picker im Eigenschaften-Panel.</summary>
    public Func<IReadOnlyList<string>>? AvailableSecrets { get; set; }

    /// <summary>Erzwingt einen Neuaufbau des Eigenschaften-Panels (z. B. nachdem der Tresor entsperrt wurde).</summary>
    public void RefreshProperties() => this.RaisePropertyChanged(nameof(SelectedNode));

}
