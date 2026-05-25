using WebExStudio.Core.Models;
using Xunit;

namespace WebExStudio.Core.Tests;

public class FlowDocument2Tests
{
    private static FlowDocument2 Sample() => new()
    {
        Tabs =
        [
            new FlowTab { Id = "main", Label = "Main", IsSubFlow = false },
            new FlowTab { Id = "t1", Label = "Login", IsSubFlow = true, Name = "login" },
            // branch tab (owned by a node) — not a standalone subnode
            new FlowTab { Id = "t2", Label = "Then", IsSubFlow = true, OwnerNodeId = "n1", Slot = "then" },
        ],
        Nodes =
        [
            new FlowNode { Id = "a", Type = "goto", TabId = "main", Wires = [["b"]] },
            new FlowNode { Id = "b", Type = "click", TabId = "main", Wires = [[]] },
            new FlowNode { Id = "c", Type = "sleep", TabId = "t1", SeqIndex = 1 },
            new FlowNode { Id = "d", Type = "sleep", TabId = "t1", SeqIndex = 0 },
        ],
    };

    [Fact]
    public void GetTabByName_FindsNamedSubnode_AndNullForUnknown()
    {
        var doc = Sample();
        Assert.Equal("t1", doc.GetTabByName("login")!.Id);
        Assert.Null(doc.GetTabByName("nope"));
    }

    [Fact]
    public void Subnodes_OnlyStandaloneNamed()
    {
        var doc = Sample();
        var names = doc.Subnodes.Select(t => t.Name).ToList();
        Assert.Equal(["login"], names); // branch tab t2 (owned) excluded
    }

    [Fact]
    public void GetNodes_SortedBySeqIndex()
    {
        var doc = Sample();
        var ids = doc.GetNodes("t1").Select(n => n.Id).ToList();
        Assert.Equal(["d", "c"], ids); // d (seq 0) before c (seq 1)
    }

    [Fact]
    public void BuildIncomingSet_TracksWireTargets()
    {
        var doc = Sample();
        var incoming = doc.BuildIncomingSet("main");
        Assert.Contains("b", incoming);
        Assert.DoesNotContain("a", incoming); // entry node has no incoming wire
    }
}
