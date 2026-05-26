using WebExStudio.Core.Models;
using Xunit;

namespace WebExStudio.Core.Tests;

public class NodeCatalogTests
{
    [Fact]
    public void Get_ReturnsKnown_AndNullForUnknown()
    {
        Assert.NotNull(NodeCatalog.Get("goto"));
        Assert.Null(NodeCatalog.Get("does_not_exist"));
    }

    [Fact]
    public void GetOrUnknown_FallsBack()
    {
        var def = NodeCatalog.GetOrUnknown("xyz");
        Assert.Equal("xyz", def.Type);
        Assert.Equal("Unbekannt", def.Category);
    }

    [Theory]
    [InlineData("if_then_else", 2, "then", "else")]
    [InlineData("foreach", 2, "Element", "fertig")]
    [InlineData("for_range", 2, "Schleife", "fertig")]
    [InlineData("get_links", 2, "je Link", "fertig")]
    public void ControlNodes_HaveTwoLabeledOutputs(string type, int ports, string p0, string p1)
    {
        var def = NodeCatalog.Get(type)!;
        Assert.Equal(ports, def.OutputPorts);
        Assert.Equal([p0, p1], def.OutputLabels);
    }

    [Fact]
    public void Annotations_HaveNoPorts()
    {
        var def = NodeCatalog.Get("note")!;
        Assert.Equal(0, def.InputPorts);
        Assert.Equal(0, def.OutputPorts);
    }

    [Fact]
    public void Quit_HasNoOutput()
    {
        Assert.Equal(0, NodeCatalog.Get("quit")!.OutputPorts);
    }

    [Fact]
    public void EveryNode_HasDescriptionAndExample()
    {
        // Versteckte/veraltete Alias-Nodes (z. B. caption/label) brauchen kein Beispiel.
        foreach (var def in NodeCatalog.All.Where(d => !d.Hidden))
        {
            Assert.False(string.IsNullOrWhiteSpace(def.Description), $"{def.Type} ohne Beschreibung");
            Assert.False(string.IsNullOrWhiteSpace(def.Example), $"{def.Type} ohne Beispiel");
        }
    }
}
