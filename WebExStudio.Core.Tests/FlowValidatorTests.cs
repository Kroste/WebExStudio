using WebExStudio.Core.Models;
using WebExStudio.Core.Validation;
using Xunit;

namespace WebExStudio.Core.Tests;

public class FlowValidatorTests
{
    /// <summary>A small, fully valid document: goto → click on the main tab.</summary>
    private static FlowDocument2 ValidDoc() => new()
    {
        Tabs = [new FlowTab { Id = "main", Label = "Main", IsSubFlow = false }],
        Nodes =
        [
            new FlowNode { Id = "a", Type = "goto", TabId = "main",
                Config = new() { ["url"] = "https://example.com" }, Wires = [["b"]] },
            new FlowNode { Id = "b", Type = "click", TabId = "main",
                Config = new() { ["selector"] = "#go" }, Wires = [[]] },
        ],
    };

    [Fact]
    public void ValidDocument_HasNoIssues()
    {
        var result = FlowValidator.Validate(ValidDoc());
        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void UnknownNodeType_IsError()
    {
        var doc = ValidDoc();
        doc.Nodes[1].Type = "teleport";
        var result = FlowValidator.Validate(doc);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "unknown-type" && e.NodeId == "b");
    }

    [Fact]
    public void MissingRequiredProperty_IsError()
    {
        var doc = ValidDoc();
        doc.Nodes[0].Config.Remove("url"); // goto.url is required
        var result = FlowValidator.Validate(doc);
        Assert.Contains(result.Errors, e => e.Code == "missing-required" && e.NodeId == "a");
    }

    [Fact]
    public void RequiredProperty_SatisfiedByAlias()
    {
        var doc = ValidDoc();
        doc.Nodes[1].Config.Remove("selector");
        doc.Nodes[1].Config["xpath"] = "//button"; // alias of click.selector
        var result = FlowValidator.Validate(doc);
        Assert.DoesNotContain(result.Issues, i => i.Code == "missing-required");
    }

    [Fact]
    public void DanglingWire_IsError()
    {
        var doc = ValidDoc();
        doc.Nodes[0].Wires = [["does-not-exist"]];
        var result = FlowValidator.Validate(doc);
        Assert.Contains(result.Errors, e => e.Code == "dangling-wire" && e.NodeId == "a");
    }

    [Fact]
    public void CrossTabWire_IsError()
    {
        var doc = ValidDoc();
        doc.Tabs.Add(new FlowTab { Id = "sub", Label = "Sub", IsSubFlow = true, Name = "sub" });
        doc.Nodes.Add(new FlowNode { Id = "c", Type = "sleep", TabId = "sub",
            Config = new() { ["seconds"] = "1" }, Wires = [[]] });
        doc.Nodes[0].Wires = [["c"]]; // main → sub: not allowed
        var result = FlowValidator.Validate(doc);
        Assert.Contains(result.Errors, e => e.Code == "cross-tab-wire" && e.NodeId == "a");
    }

    [Fact]
    public void WireOnNonexistentPort_IsError()
    {
        var doc = ValidDoc();
        // click has a single output port; put a wire on port 1.
        doc.Nodes[1].Wires = [[], ["a"]];
        var result = FlowValidator.Validate(doc);
        Assert.Contains(result.Errors, e => e.Code == "wire-invalid-port" && e.NodeId == "b");
    }

    [Fact]
    public void WireIntoAnnotation_IsError()
    {
        var doc = ValidDoc();
        doc.Nodes.Add(new FlowNode { Id = "note", Type = "label", TabId = "main",
            Config = new() { ["text"] = "Hi" }, Wires = [[]] });
        doc.Nodes[1].Wires = [["note"]]; // click → label (label has no input port)
        var result = FlowValidator.Validate(doc);
        Assert.Contains(result.Errors, e => e.Code == "wire-into-no-input" && e.NodeId == "b");
    }

    [Fact]
    public void CallToMissingSubnode_IsError()
    {
        var doc = ValidDoc();
        doc.Nodes[1].Wires = [[]];
        doc.Nodes.Add(new FlowNode { Id = "c", Type = "call", TabId = "main",
            Config = new() { ["target"] = "nirgendwo" }, Wires = [[]] });
        doc.Nodes[1].Wires = [["c"]];
        var result = FlowValidator.Validate(doc);
        Assert.Contains(result.Errors, e => e.Code == "call-target-missing" && e.NodeId == "c");
    }

    [Fact]
    public void CallToExistingSubnode_IsValid()
    {
        var doc = ValidDoc();
        doc.Tabs.Add(new FlowTab { Id = "sub", Label = "Login", IsSubFlow = true, Name = "login" });
        doc.Nodes.Add(new FlowNode { Id = "s", Type = "sleep", TabId = "sub",
            Config = new() { ["seconds"] = "1" }, Wires = [[]] });
        doc.Nodes.Add(new FlowNode { Id = "c", Type = "call", TabId = "main",
            Config = new() { ["target"] = "login" }, Wires = [[]] });
        doc.Nodes[1].Wires = [["c"]];
        var result = FlowValidator.Validate(doc);
        Assert.DoesNotContain(result.Issues, i => i.Code == "call-target-missing");
        Assert.True(result.IsValid);
    }

    [Fact]
    public void DuplicateNodeId_IsError()
    {
        var doc = ValidDoc();
        doc.Nodes.Add(new FlowNode { Id = "a", Type = "sleep", TabId = "main",
            Config = new() { ["seconds"] = "1" }, Wires = [[]] });
        var result = FlowValidator.Validate(doc);
        Assert.Contains(result.Errors, e => e.Code == "duplicate-node-id");
    }

    [Fact]
    public void DuplicateSubnodeName_IsError()
    {
        var doc = ValidDoc();
        doc.Tabs.Add(new FlowTab { Id = "s1", Label = "A", IsSubFlow = true, Name = "dup" });
        doc.Tabs.Add(new FlowTab { Id = "s2", Label = "B", IsSubFlow = true, Name = "dup" });
        var result = FlowValidator.Validate(doc);
        Assert.Contains(result.Errors, e => e.Code == "duplicate-subnode-name");
    }

    [Fact]
    public void NoMainTab_IsError()
    {
        var doc = new FlowDocument2
        {
            Tabs = [new FlowTab { Id = "sub", Label = "Sub", IsSubFlow = true, Name = "sub" }],
        };
        var result = FlowValidator.Validate(doc);
        Assert.Contains(result.Errors, e => e.Code == "no-main-tab");
    }

    [Fact]
    public void UnknownTabReference_IsError()
    {
        var doc = ValidDoc();
        doc.Nodes[1].TabId = "ghost";
        var result = FlowValidator.Validate(doc);
        Assert.Contains(result.Errors, e => e.Code == "unknown-tab" && e.NodeId == "b");
    }

    [Fact]
    public void CycleWithoutEntry_IsWarning()
    {
        var doc = ValidDoc();
        doc.Nodes[0].Wires = [["b"]];
        doc.Nodes[1].Wires = [["a"]]; // a ↔ b, neither is an entry
        var result = FlowValidator.Validate(doc);
        Assert.True(result.IsValid); // only a warning
        Assert.Contains(result.Warnings, w => w.Code == "no-entry-node");
    }

    [Fact]
    public void GroupWithMissingNode_IsWarning()
    {
        var doc = ValidDoc();
        doc.Groups.Add(new FlowGroup { Id = "g", TabId = "main", Label = "G", NodeIds = ["a", "ghost"] });
        var result = FlowValidator.Validate(doc);
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Code == "group-missing-node");
    }
}
