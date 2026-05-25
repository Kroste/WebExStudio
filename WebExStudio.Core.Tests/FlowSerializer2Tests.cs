using WebExStudio.Core.Models;
using WebExStudio.Core.Serialization;
using Xunit;

namespace WebExStudio.Core.Tests;

public class FlowSerializer2Tests
{
    [Fact]
    public void CreateEmpty_HasSingleMainTab()
    {
        var doc = FlowSerializer2.CreateEmpty();
        var main = Assert.Single(doc.Tabs);
        Assert.False(main.IsSubFlow);
        Assert.Empty(doc.Nodes);
        Assert.Equal(2, doc.Version);
    }

    [Fact]
    public void Serialize_Deserialize_RoundTrips()
    {
        var doc = new FlowDocument2
        {
            Tabs = [new FlowTab { Id = "main", Label = "Main", IsSubFlow = false }],
            Nodes =
            [
                new FlowNode { Id = "a", Type = "goto", TabId = "main", Label = "Start",
                    Config = new() { ["url"] = "https://example.com" }, Wires = [["b"]] },
                new FlowNode { Id = "b", Type = "click", TabId = "main",
                    Config = new() { ["selector"] = "#go" }, Wires = [[]] },
            ],
        };

        var json = FlowSerializer2.Serialize(doc);
        var back = FlowSerializer2.Deserialize(json);

        Assert.Equal(2, back.Nodes.Count);
        var a = back.GetNode("a")!;
        Assert.Equal("goto", a.Type);
        Assert.Equal("Start", a.Label);
        Assert.Equal("https://example.com", a.Get("url"));
        Assert.Contains("b", a.Wires[0]);
    }

    [Fact]
    public async Task SaveLoad_RoundTrips_Nodes_Wires_Config_Label()
    {
        var doc = new FlowDocument2
        {
            Tabs =
            [
                new FlowTab { Id = "main", Label = "Main", IsSubFlow = false },
                new FlowTab { Id = "t1", Label = "Login", IsSubFlow = true, Name = "login" },
            ],
            Nodes =
            [
                new FlowNode
                {
                    Id = "n1", Type = "goto", TabId = "main", Label = "Startseite",
                    X = 80, Y = 40,
                    Config = new() { ["url"] = "{payload.host}", ["wait_ms"] = "500" },
                    Wires = [["n2"]],
                },
                new FlowNode
                {
                    Id = "n2", Type = "if_then_else", TabId = "main", X = 80, Y = 160,
                    Config = new() { ["condition"] = "element_exists", ["selector"] = "#x" },
                    Wires = [["n3"], []],
                },
                new FlowNode { Id = "n3", Type = "click", TabId = "t1", X = 0, Y = 0, Config = new() { ["selector"] = "#go" } },
            ],
        };

        var path = Path.Combine(Path.GetTempPath(), $"webex_{Guid.NewGuid():N}.json");
        try
        {
            await FlowSerializer2.SaveAsync(doc, path);
            var loaded = await FlowSerializer2.LoadAsync(path);

            Assert.Equal(2, loaded.Tabs.Count);
            Assert.Equal("login", loaded.GetTabByName("login")!.Name);
            Assert.Equal(3, loaded.Nodes.Count);

            var n1 = loaded.GetNode("n1")!;
            Assert.Equal("goto", n1.Type);
            Assert.Equal("Startseite", n1.Label);
            Assert.Equal("{payload.host}", n1.Config["url"]);
            Assert.Equal(new[] { "n2" }, n1.Wires[0]);

            var n2 = loaded.GetNode("n2")!;
            Assert.Equal(2, n2.Wires.Count);                 // then/else ports preserved
            Assert.Equal(new[] { "n3" }, n2.Wires[0]);
            Assert.Empty(n2.Wires[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Load_CoercesNumberConfigValuesToStrings()
    {
        // wait_ms written as a JSON number must load as a string (StringValueDictionaryConverter).
        var json = """
        { "version": 2,
          "tabs": [{ "id": "main", "label": "Main", "isSubFlow": false }],
          "nodes": [{ "id": "n1", "type": "goto", "tabId": "main",
                      "config": { "wait_ms": 500, "scroll": true }, "wires": [[]] }] }
        """;
        var path = Path.Combine(Path.GetTempPath(), $"webex_{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, json);
        try
        {
            var loaded = await FlowSerializer2.LoadAsync(path);
            var n = loaded.GetNode("n1")!;
            Assert.Equal("500", n.Config["wait_ms"]);
            Assert.Equal("true", n.Config["scroll"]);
        }
        finally { File.Delete(path); }
    }
}
