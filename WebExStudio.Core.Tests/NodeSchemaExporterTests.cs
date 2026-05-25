using System.Text.Json;
using WebExStudio.Core.Ai;
using WebExStudio.Core.Models;
using Xunit;

namespace WebExStudio.Core.Tests;

public class NodeSchemaExporterTests
{
    [Fact]
    public void Export_CoversEveryCatalogType()
    {
        var schema = NodeSchemaExporter.Export();
        Assert.Equal(NodeCatalog.All.Count, schema.Count);
        Assert.All(NodeCatalog.All, def =>
            Assert.Contains(schema, s => s.Type == def.Type));
    }

    [Fact]
    public void Export_CarriesPortSemantics()
    {
        var schema = NodeSchemaExporter.Export();

        var ifNode = schema.Single(s => s.Type == "if_then_else");
        Assert.Equal(["then", "else"], ifNode.Outputs);

        var foreachNode = schema.Single(s => s.Type == "foreach");
        Assert.Equal(["Element", "fertig"], foreachNode.Outputs);

        // Single-output node gets a generic label; annotations have none.
        Assert.Equal(["weiter"], schema.Single(s => s.Type == "goto").Outputs);
        Assert.Empty(schema.Single(s => s.Type == "label").Outputs);
    }

    [Fact]
    public void Export_MarksRequiredPropertiesAndAliases()
    {
        var schema = NodeSchemaExporter.Export();

        var url = schema.Single(s => s.Type == "goto").Properties.Single(p => p.Key == "url");
        Assert.True(url.Required);

        var selector = schema.Single(s => s.Type == "click").Properties.Single(p => p.Key == "selector");
        Assert.True(selector.Required);
        Assert.NotNull(selector.Aliases);
        Assert.Contains("xpath", selector.Aliases!);
    }

    [Fact]
    public void ToJson_ProducesParseableJson()
    {
        var json = NodeSchemaExporter.ToJson();
        using var doc = JsonDocument.Parse(json); // throws if invalid
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.NotEqual(0, doc.RootElement.GetArrayLength());
    }
}
