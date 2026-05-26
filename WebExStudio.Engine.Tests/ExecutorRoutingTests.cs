using WebExStudio.Core.Models;
using WebExStudio.Engine;
using Xunit;

namespace WebExStudio.Engine.Tests;

/// <summary>
/// Exercises the wire-following executor without a browser: function/set_payload/if(payload)/foreach
/// don't touch the page, so RunTabAsync can run them with a null page.
/// </summary>
public class ExecutorRoutingTests
{
    private static FlowDocument2 Doc(params FlowNode[] nodes) => new()
    {
        Tabs = [new FlowTab { Id = "main", Label = "Main", IsSubFlow = false }],
        Nodes = nodes.ToList(),
    };

    private static FlowNode N(string id, string type, Dictionary<string, string> cfg, params string[][] wires) =>
        new() { Id = id, Type = type, TabId = "main", Config = cfg, Wires = wires.Select(w => w.ToList()).ToList() };

    private static async Task<TraceRecorder> Run(FlowDocument2 doc)
    {
        var rec = new TraceRecorder();
        await new FlowExecutor().RunTabAsync(doc, "main", page: null!, new RunConfig(),
            new TargetConfig { Name = "t" }, rec);
        return rec;
    }

    [Fact]
    public async Task If_TakesThenBranch_WhenConditionTrue()
    {
        var doc = Doc(
            N("f", "function", new() { ["payload"] = """{ "status": "ok" }""" }, ["if1"]),
            N("if1", "if_then_else", new() { ["condition"] = "payload_equals", ["selector"] = "status", ["value"] = "ok" },
                ["tn"], ["en"]),
            N("tn", "set_payload", new() { ["key"] = "b", ["value"] = "THEN" }),
            N("en", "set_payload", new() { ["key"] = "b", ["value"] = "ELSE" }));

        var rec = await Run(doc);
        Assert.True(rec.Ran("tn"));
        Assert.False(rec.Ran("en"));
    }

    [Fact]
    public async Task If_TakesElseBranch_WhenConditionFalse()
    {
        var doc = Doc(
            N("f", "function", new() { ["payload"] = """{ "status": "nope" }""" }, ["if1"]),
            N("if1", "if_then_else", new() { ["condition"] = "payload_equals", ["selector"] = "status", ["value"] = "ok" },
                ["tn"], ["en"]),
            N("tn", "set_payload", new() { ["key"] = "b", ["value"] = "THEN" }),
            N("en", "set_payload", new() { ["key"] = "b", ["value"] = "ELSE" }));

        var rec = await Run(doc);
        Assert.False(rec.Ran("tn"));
        Assert.True(rec.Ran("en"));
    }

    [Fact]
    public async Task If_PayloadContains_FallsBackToKey_WhenSelectorEmpty()
    {
        // Regressionstest: payload_contains liest den Schlüssel aus 'selector'. Ist dieser leer,
        // muss auf 'key' zurückgefallen werden (sonst war die Bedingung still immer false →
        // f95-„Link bereits besucht?" sprang immer in den else-Zweig).
        var doc = Doc(
            N("f", "function", new() { ["payload"] = """{ "visited": "/threads/a/\n/threads/b/" }""" }, ["seed"]),
            N("seed", "set_payload", new() { ["key"] = "link", ["value"] = "/threads/b/" }, ["if1"]),
            N("if1", "if_then_else",
                new() { ["condition"] = "payload_contains", ["selector"] = "", ["key"] = "visited", ["value"] = "{payload.link}" },
                ["tn"], ["en"]),
            N("tn", "set_payload", new() { ["key"] = "b", ["value"] = "THEN" }),
            N("en", "set_payload", new() { ["key"] = "b", ["value"] = "ELSE" }));

        var rec = await Run(doc);
        Assert.True(rec.Ran("tn"));  // Link ist in 'visited' enthalten → then-Zweig
        Assert.False(rec.Ran("en"));
    }

    [Fact]
    public async Task Foreach_RunsBodyPerItem_ThenDoneOnce()
    {
        var doc = Doc(
            N("f", "function", new() { ["payload"] = """{ "items": [ {"k":"a"}, {"k":"b"}, {"k":"c"} ] }""" }, ["fe"]),
            N("fe", "foreach", new() { ["items"] = "{payload.items}", ["ctx_key"] = "item" },
                ["body"], ["done"]),
            N("body", "set_payload", new() { ["key"] = "x", ["value"] = "y" }),
            N("done", "set_payload", new() { ["key"] = "z", ["value"] = "w" }));

        var rec = await Run(doc);
        Assert.Equal(3, rec.Running("body")); // once per item
        Assert.Equal(1, rec.Running("done")); // done output once
    }

    [Fact]
    public async Task Foreach_SpreadsObjectFieldsIntoPayload()
    {
        // Each item {"k":..} is spread into the payload; the debug node prints only key "k".
        var rec = new TraceRecorder();
        var doc = Doc(
            N("f", "function", new() { ["payload"] = """{ "items": [ {"k":"a"}, {"k":"b"} ] }""" }, ["fe"]),
            N("fe", "foreach", new() { ["items"] = "{payload.items}", ["ctx_key"] = "item" }, ["dbg"], []),
            N("dbg", "debug", new() { ["source"] = "payload", ["key"] = "k" }));

        await new FlowExecutor().RunTabAsync(doc, "main", page: null!, new RunConfig(),
            new TargetConfig { Name = "t" }, rec);

        var msgs = rec.Entries.Where(e => e.NodeId == "dbg" && e.Message is not null).Select(e => e.Message!).ToList();
        Assert.Contains(msgs, m => m.Contains("k=a")); // debug key-filter prints "k=<value>"
        Assert.Contains(msgs, m => m.Contains("k=b"));
    }
}
