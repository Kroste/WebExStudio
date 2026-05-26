using WebExStudio.Core.Models;
using WebExStudio.Engine.Actions;
using WebExStudio.Engine.Plugins;
using Xunit;

namespace WebExStudio.Engine.Tests;

/// <summary>Ein Test-Plugin, das in dieser Assembly definiert ist (statt einer externen DLL).</summary>
public sealed class SampleNodePlugin : INodePlugin
{
    public IEnumerable<NodePluginNode> CreateNodes() =>
    [
        new NodePluginNode(
            new NodeDefinition
            {
                Type = "sample_plugin_node",
                DisplayName = "Beispiel-Plugin-Node",
                Category = "Plugins",
                Description = "Vom Test-Plugin.",
                Example = "—",
            },
            new SampleHandler()),
    ];

    private sealed class SampleHandler : IActionHandler
    {
        public string Type => "sample_plugin_node";
        public Task ExecuteAsync(ExecutionContext ctx, FlowNode node) => Task.CompletedTask;
    }
}

public class NodePluginTests
{
    [Fact]
    public void LoadFromAssembly_FindsPluginNodes()
    {
        var nodes = NodePluginLoader.LoadFromAssembly(typeof(SampleNodePlugin).Assembly);
        Assert.Contains(nodes, n => n.Definition.Type == "sample_plugin_node" && n.Handler.Type == "sample_plugin_node");
    }

    [Fact]
    public void LoadFromDirectory_MissingDir_ReturnsEmpty()
    {
        Assert.Empty(NodePluginLoader.LoadFromDirectory("/gibt/es/nicht/plugins"));
    }

    [Fact]
    public void Catalog_Register_AddsNew_ButNotDuplicateType()
    {
        var def = new NodeDefinition { Type = "unit_plugin_xyz", DisplayName = "X", Category = "Plugins", Description = "d", Example = "e" };
        Assert.True(NodeCatalog.Register(def));
        Assert.Equal("X", NodeCatalog.Get("unit_plugin_xyz")!.DisplayName);
        Assert.False(NodeCatalog.Register(def));      // schon vorhanden
        Assert.False(NodeCatalog.Register(new NodeDefinition { Type = "click" })); // Built-in nicht überschreibbar
    }

    [Fact]
    public void RegisterPlugin_Handler_AppearsInDefaultRegistry()
    {
        ActionRegistry.RegisterPlugin(new RegPluginHandler());
        Assert.NotNull(ActionRegistry.CreateDefault().Get("reg_plugin_handler"));
    }

    private sealed class RegPluginHandler : IActionHandler
    {
        public string Type => "reg_plugin_handler";
        public Task ExecuteAsync(ExecutionContext ctx, FlowNode node) => Task.CompletedTask;
    }
}
