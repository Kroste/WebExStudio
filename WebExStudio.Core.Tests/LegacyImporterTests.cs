using WebExStudio.Core.Models;
using WebExStudio.Core.Serialization;
using Xunit;

namespace WebExStudio.Core.Tests;

public class LegacyImporterTests : IDisposable
{
    private readonly string _dir;

    public LegacyImporterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"webex_legacy_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_dir, "actions"));

        Write("targets.json", """
        [ { "name": "T1", "host": "10.0.0.1", "actions_file": "actions/start.json",
            "ctx": { "location": "Ort", "seconds": 2 } } ]
        """);
        Write("actions/start.json", """
        { "actions": [
            { "type": "goto", "url": "https://{host}/" },
            { "type": "if_then_else",
              "condition": { "extract": "page_text" }, "op": "matches", "value": ".+",
              "then_actions_file": "actions/usv.json", "else": [] } ] }
        """);
        Write("actions/usv.json", """
        { "actions": [
            { "type": "send_keys", "name": "login_user", "value": "x" },
            { "type": "call", "actions_file": "actions/submit.json" } ] }
        """);
        Write("actions/submit.json", """
        { "actions": [ { "type": "click", "name": "submit" } ] }
        """);
    }

    private void Write(string rel, string content) =>
        File.WriteAllText(Path.Combine(_dir, rel), content);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Convert_BuildsMainFunctionForeachAndNamedSubnodes()
    {
        var doc = LegacyImporter.Convert(_dir);

        // Main: function → foreach
        var main = doc.Tabs.Single(t => !t.IsSubFlow);
        var mainNodes = doc.Nodes.Where(n => n.TabId == main.Id).ToList();
        Assert.Contains(mainNodes, n => n.Type == "function");
        var foreachNode = Assert.Single(mainNodes, n => n.Type == "foreach");

        // Subnodes for each referenced file
        Assert.NotNull(doc.GetTabByName("start"));
        Assert.NotNull(doc.GetTabByName("usv"));
        Assert.NotNull(doc.GetTabByName("submit"));

        // foreach "Element" output → call(start)
        var bodyTargetId = Assert.Single(foreachNode.Wires[0]);
        var callStart = doc.GetNode(bodyTargetId)!;
        Assert.Equal("call", callStart.Type);
        Assert.Equal("start", callStart.Get("target"));
    }

    [Fact]
    public void Convert_IfHasThenElsePorts_AndUsvCallsSubmit()
    {
        var doc = LegacyImporter.Convert(_dir);

        // The if in 'start' has 2 output ports; its 'then' (port 0) leads to call(usv).
        var start = doc.GetTabByName("start")!;
        var ifNode = Assert.Single(doc.Nodes, n => n.TabId == start.Id && n.Type == "if_then_else");
        Assert.True(ifNode.Wires.Count >= 1);
        var thenTarget = doc.GetNode(ifNode.Wires[0][0])!;
        Assert.Equal("call", thenTarget.Type);
        Assert.Equal("usv", thenTarget.Get("target"));

        // usv contains a call(submit)
        var usv = doc.GetTabByName("usv")!;
        Assert.Contains(doc.Nodes,
            n => n.TabId == usv.Id && n.Type == "call" && n.Get("target") == "submit");
    }

    [Fact]
    public void Convert_FunctionPayloadContainsTargets()
    {
        var doc = LegacyImporter.Convert(_dir);
        var func = doc.Nodes.Single(n => n.Type == "function");
        var payload = func.Get("payload");
        Assert.Contains("targets", payload);
        Assert.Contains("10.0.0.1", payload);
        Assert.Contains("Ort", payload); // ctx.location flattened in
    }
}
