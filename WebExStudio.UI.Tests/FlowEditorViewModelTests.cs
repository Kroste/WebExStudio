using WebExStudio.UI.ViewModels;
using Xunit;

namespace WebExStudio.UI.Tests;

/// <summary>
/// Pure ViewModel logic — no Avalonia rendering / dispatcher required.
/// </summary>
public class FlowEditorViewModelTests
{
    private static FlowEditorViewModel NewEditor()
    {
        var vm = new FlowEditorViewModel();
        vm.NewDocument();
        return vm;
    }

    [Fact]
    public void UndoRedo_RestoresAddedNode()
    {
        var vm = NewEditor();
        var before = vm.ActiveTab!.Nodes.Count;
        vm.PushUndo();
        vm.AddNode("click", 0, 0);
        Assert.Equal(before + 1, vm.ActiveTab!.Nodes.Count);
        Assert.True(vm.CanUndo);

        vm.Undo();
        Assert.Equal(before, vm.ActiveTab!.Nodes.Count);
        Assert.True(vm.CanRedo);

        vm.Redo();
        Assert.Equal(before + 1, vm.ActiveTab!.Nodes.Count);
    }

    [Fact]
    public void DuplicateSelection_AddsCopy()
    {
        var vm = NewEditor();
        var n = vm.AddNode("click", 50, 50); // wird ausgewählt
        var before = vm.ActiveTab!.Nodes.Count;
        vm.DuplicateSelection();
        Assert.Equal(before + 1, vm.ActiveTab!.Nodes.Count);
        Assert.NotEqual(n.Id, vm.SelectedNode!.Id); // Auswahl auf der Kopie
    }

    [Fact]
    public void CopyPaste_AddsCopyIntoActiveTab()
    {
        var vm = NewEditor();
        vm.AddNode("click", 0, 0);
        vm.CopySelection();
        Assert.True(vm.HasClipboard);
        var before = vm.ActiveTab!.Nodes.Count;
        vm.PasteClipboard();
        Assert.Equal(before + 1, vm.ActiveTab!.Nodes.Count);
    }

    [Fact]
    public void AutoLayout_PlacesConnectedNodesTopDown()
    {
        var vm = NewEditor();
        var a = vm.AddNode("goto", 999, 999);
        var b = vm.AddNode("click", 0, 0);
        vm.AddWire(a.Id, 0, b.Id);
        vm.AutoLayoutActiveTab();
        Assert.True(b.Y > a.Y); // Ziel liegt unter der Quelle
    }

    [Fact]
    public void SnapAllToGrid_RoundsNodePositions()
    {
        var vm = NewEditor();
        var n = vm.AddNode("click", 33, 51);
        vm.SnapAllToGrid(20);
        Assert.Equal(40, n.X);
        Assert.Equal(60, n.Y);
    }

    [Fact]
    public void MoveSelectedBy_ShiftsSelectedNodes()
    {
        var vm = NewEditor();
        var n = vm.AddNode("click", 100, 100); // wird automatisch ausgewählt
        vm.MoveSelectedBy(20, -10);
        Assert.Equal(120, n.X);
        Assert.Equal(90, n.Y);
    }

    [Fact]
    public void OpenSubnodeByName_OpensMatchingSubnode_AsActiveTab()
    {
        var vm = NewEditor();
        var sub = vm.CreateSubnode("assets", "f95.Assets");
        Assert.NotNull(sub);
        // Doppelklick-Logik: call-Node referenziert den Subnode über 'target'.
        Assert.True(vm.OpenSubnodeByName("assets"));
        Assert.Equal(sub, vm.ActiveTab);
        Assert.Contains(sub!, vm.OpenTabs);
    }

    [Fact]
    public void OpenSubnodeByName_ReturnsFalse_ForUnknownOrEmpty()
    {
        var vm = NewEditor();
        vm.CreateSubnode("assets", "f95.Assets");
        Assert.False(vm.OpenSubnodeByName("does-not-exist"));
        Assert.False(vm.OpenSubnodeByName(""));
        Assert.False(vm.OpenSubnodeByName(null));
    }

    [Fact]
    public void NewDocument_HasMainTab_OpenAndActive()
    {
        var vm = NewEditor();
        Assert.NotNull(vm.Document);
        Assert.True(vm.CanSave);
        Assert.NotNull(vm.ActiveTab);
        Assert.False(vm.ActiveTab!.IsSubFlow);
        Assert.Contains(vm.ActiveTab, vm.OpenTabs);
    }

    [Fact]
    public void AddNode_AddsToActiveTab_SelectsIt_MarksDirty()
    {
        var vm = NewEditor();
        var node = vm.AddNode("click", 100, 100);

        Assert.Contains(node, vm.Nodes);
        Assert.Same(node, vm.SelectedNode);
        Assert.True(vm.IsDirty);
        Assert.Equal("click", node.ActionType);
    }

    [Fact]
    public void AddWire_And_RemoveWire()
    {
        var vm = NewEditor();
        var a = vm.AddNode("goto", 0, 0);
        var b = vm.AddNode("click", 0, 120);

        Assert.True(vm.AddWire(a.Id, 0, b.Id));
        var wire = Assert.Single(vm.Wires);
        Assert.Equal(a.Id, wire.SourceNodeId);
        Assert.Equal(b.Id, wire.TargetNodeId);
        Assert.Contains(b.Id, a.Model.Wires[0]);

        // duplicate / self-wire rejected
        Assert.False(vm.AddWire(a.Id, 0, b.Id));
        Assert.False(vm.AddWire(a.Id, 0, a.Id));

        vm.RemoveWire(wire);
        Assert.Empty(vm.Wires);
        Assert.DoesNotContain(b.Id, a.Model.Wires[0]);
    }

    [Fact]
    public void Node_Label_RoundTrips()
    {
        var vm = NewEditor();
        var n = vm.AddNode("click", 0, 0);
        Assert.False(n.HasLabel);
        n.Label = "Login-Button";
        Assert.True(n.HasLabel);
        Assert.Equal("Login-Button", n.Model.Label);
    }

    [Fact]
    public void CreateSubnode_AddsToList_OpensIt_AppearsInNames()
    {
        var vm = NewEditor();
        var sub = vm.CreateSubnode("login", "Login");

        Assert.NotNull(sub);
        Assert.Contains(sub!, vm.Subnodes);
        Assert.Contains("login", vm.SubnodeNames);
        Assert.Contains(sub, vm.OpenTabs);     // opened
        Assert.Same(sub, vm.ActiveTab);

        // duplicate name rejected
        Assert.Null(vm.CreateSubnode("login", "Andere"));
    }

    [Fact]
    public void RenameSubnode_UpdatesNameAndLabel()
    {
        var vm = NewEditor();
        var sub = vm.CreateSubnode("a", "A")!;
        vm.RenameSubnode(sub, "b", "B");
        Assert.Equal("b", sub.Name);
        Assert.Equal("B", sub.Label);
        Assert.Contains("b", vm.SubnodeNames);
        Assert.DoesNotContain("a", vm.SubnodeNames);
    }

    [Fact]
    public void DeleteSubnode_RemovesEverywhere()
    {
        var vm = NewEditor();
        var sub = vm.CreateSubnode("temp", "Temp")!;
        vm.DeleteSubnode(sub);
        Assert.DoesNotContain(sub, vm.Subnodes);
        Assert.DoesNotContain("temp", vm.SubnodeNames);
        Assert.DoesNotContain(sub, vm.OpenTabs);
    }

    [Fact]
    public void FindTabOfNode_LocatesOwningTab()
    {
        var vm = NewEditor();
        var n = vm.AddNode("goto", 0, 0);
        var tab = vm.FindTabOfNode(n.Id);
        Assert.Same(vm.ActiveTab, tab);
        Assert.Null(vm.FindTabOfNode("does-not-exist"));
    }

    [Fact]
    public void CloseTab_RemovesSubnode_KeepsMain()
    {
        var vm = NewEditor();
        var main = vm.ActiveTab!;
        var sub = vm.CreateSubnode("s1", "S1")!; // opens + active

        vm.CloseTab(sub);
        Assert.DoesNotContain(sub, vm.OpenTabs);
        Assert.Same(main, vm.ActiveTab);  // fell back to main

        vm.CloseTab(main);                // main cannot be closed
        Assert.Contains(main, vm.OpenTabs);
    }
}
