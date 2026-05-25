namespace WebExStudio.Core.Models;

/// <summary>
/// A visual grouping of nodes on a tab. Purely cosmetic (does not affect execution):
/// draws a labeled, colored box around its member nodes. Can be extracted into a subnode.
/// </summary>
public sealed class FlowGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string TabId { get; set; } = string.Empty;
    public string Label { get; set; } = "Gruppe";
    public string Color { get; set; } = "#455A64";
    public List<string> NodeIds { get; set; } = [];
}
