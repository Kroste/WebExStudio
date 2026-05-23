using System.Collections.ObjectModel;
using WebExStudio.Core.Models;

namespace WebExStudio.UI.ViewModels;

/// <summary>
/// UI view model for a flow tab (main canvas or sequential sub-flow).
/// </summary>
public sealed class FlowTabViewModel
{
    public FlowTab Model { get; }
    public string Id => Model.Id;
    public string Label => Model.Label;
    public bool IsSubFlow => Model.IsSubFlow;
    public string? OwnerNodeId => Model.OwnerNodeId;
    public string? Slot => Model.Slot;

    public ObservableCollection<NodeViewModel> Nodes { get; } = [];

    public FlowTabViewModel(FlowTab model) => Model = model;
}
