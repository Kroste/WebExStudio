using Avalonia;
using ReactiveUI;
using WebExStudio.Core.Models;

namespace WebExStudio.UI.ViewModels;

/// <summary>UI view model for a visual node group on a tab.</summary>
public sealed class GroupViewModel : ViewModelBase
{
    public FlowGroup Model { get; }
    public string Id => Model.Id;
    public string Label => Model.Label;
    public string Color => Model.Color;
    public IReadOnlyList<string> NodeIds => Model.NodeIds;

    public GroupViewModel(FlowGroup model) => Model = model;

    public void NotifyLabelChanged() => this.RaisePropertyChanged(nameof(Label));

    /// <summary>Bounding box (world coords) of the member nodes, or empty if none present.</summary>
    public Rect Bounds(IReadOnlyDictionary<string, NodeViewModel> nodesById)
    {
        var members = Model.NodeIds
            .Select(id => nodesById.TryGetValue(id, out var n) ? n : null)
            .Where(n => n is not null)
            .Select(n => n!)
            .ToList();
        if (members.Count == 0) return default;

        var minX = members.Min(n => n.X);
        var minY = members.Min(n => n.Y);
        var maxX = members.Max(n => n.X + n.Width);
        var maxY = members.Max(n => n.Y + n.Height);
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }
}
