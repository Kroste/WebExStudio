using WebExStudio.UI.ViewModels;
using Xunit;

namespace WebExStudio.UI.Tests;

public class GroupExtractTests
{
    private static FlowEditorViewModel NewEditor()
    {
        var vm = new FlowEditorViewModel();
        vm.NewDocument();
        return vm;
    }

    [Fact]
    public void CreateGroupFromSelection_GroupsSelectedNodes()
    {
        var vm = NewEditor();
        var a = vm.AddNode("click", 0, 0);
        var b = vm.AddNode("click", 0, 120);
        vm.SelectNode(a);
        vm.SelectNode(b, additive: true);

        var g = vm.CreateGroupFromSelection("Meine Gruppe");
        Assert.NotNull(g);
        Assert.Equal(2, g!.NodeIds.Count);
        Assert.Contains(g, vm.Groups);
    }

    [Fact]
    public void ExtractGroupToSubnode_MovesMembers_AddsCall_RewiresExternals()
    {
        var vm = NewEditor();
        // before → m1 → m2 → after   (group = {m1, m2})
        var before = vm.AddNode("goto", 0, 0);
        var m1 = vm.AddNode("click", 0, 120);
        var m2 = vm.AddNode("click", 0, 240);
        var after = vm.AddNode("sleep", 0, 360);

        Assert.True(vm.AddWire(before.Id, 0, m1.Id));
        Assert.True(vm.AddWire(m1.Id, 0, m2.Id));
        Assert.True(vm.AddWire(m2.Id, 0, after.Id));

        var mainTabId = vm.ActiveTab!.Id;
        var beforeId = before.Id; var m1Id = m1.Id; var m2Id = m2.Id; var afterId = after.Id;

        vm.SelectNode(m1);
        vm.SelectNode(m2, additive: true);
        var group = vm.CreateGroupFromSelection("Mitte")!;

        var sub = vm.ExtractGroupToSubnode(group, "mitte", "Mitte");
        Assert.NotNull(sub);
        var doc = vm.Document!;

        // members moved into the subnode
        Assert.Equal(sub!.Id, doc.GetNode(m1Id)!.TabId);
        Assert.Equal(sub.Id, doc.GetNode(m2Id)!.TabId);

        // a single call node was added on the original tab, targeting the subnode
        var call = Assert.Single(doc.Nodes, n => n.TabId == mainTabId && n.Type == "call");
        Assert.Equal("mitte", call.Get("target"));

        // external incoming wire 'before' now points at the call node
        Assert.Contains(call.Id, doc.GetNode(beforeId)!.Wires[0]);
        Assert.DoesNotContain(m1Id, doc.GetNode(beforeId)!.Wires[0]);

        // external outgoing wire now emits from the call node to 'after'
        Assert.Contains(afterId, call.Wires[0]);
        Assert.DoesNotContain(afterId, doc.GetNode(m2Id)!.Wires[0]);

        // internal wire m1 → m2 is preserved inside the subnode
        Assert.Contains(m2Id, doc.GetNode(m1Id)!.Wires[0]);

        // the group is gone
        Assert.Empty(doc.Groups);
        // 'after' still on the main tab
        Assert.Equal(mainTabId, doc.GetNode(afterId)!.TabId);
    }

    [Fact]
    public void ExtractGroupToSubnode_RejectsDuplicateName()
    {
        var vm = NewEditor();
        vm.CreateSubnode("taken", "Taken");
        var n = vm.AddNode("click", 0, 0);
        vm.SelectNode(n);
        var g = vm.CreateGroupFromSelection("G")!;
        Assert.Null(vm.ExtractGroupToSubnode(g, "taken", "X"));
    }
}
