using System.Collections.ObjectModel;
using ReactiveUI;
using WebExStudio.Core.Models;

namespace WebExStudio.UI.ViewModels;

/// <summary>
/// UI view model for a flow tab (main canvas, named subnode, or block-node branch).
/// </summary>
public sealed class FlowTabViewModel : ViewModelBase
{
    public FlowTab Model { get; }
    public string Id => Model.Id;
    public string Label => Model.Label;
    public string? Name => Model.Name;
    public bool IsSubFlow => Model.IsSubFlow;
    public string? OwnerNodeId => Model.OwnerNodeId;
    public string? Slot => Model.Slot;

    /// <summary>A standalone named subnode (reusable, referenced by call) — not a branch tab.</summary>
    public bool IsSubnode => IsSubFlow && OwnerNodeId is null && !string.IsNullOrEmpty(Name);

    /// <summary>The main canvas tab cannot be closed.</summary>
    public bool CanClose => IsSubFlow;

    public ObservableCollection<NodeViewModel> Nodes { get; } = [];

    public FlowTabViewModel(FlowTab model) => Model = model;

    public void NotifyLabelChanged()
    {
        this.RaisePropertyChanged(nameof(Label));
        this.RaisePropertyChanged(nameof(Name));
    }
}
