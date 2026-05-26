using WebExStudio.Core.Models;
using WebExStudio.Engine.Actions;
using Xunit;

namespace WebExStudio.Engine.Tests;

/// <summary>Handlers that don't touch the browser page can be tested directly.</summary>
public class HandlerTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(1, false)]
    [InlineData(120, false)]
    public void CaptchaGuard_TimeoutZeroOrNegative_MeansUnlimited(int timeoutSec, bool expected) =>
        Assert.Equal(expected, CaptchaGuardHandler.IsUnlimitedTimeout(timeoutSec));

    [Fact]
    public async Task SetPayload_WritesKey()
    {
        var ctx = Ctx.Make();
        var node = new FlowNode { Type = "set_payload", Config = new() { ["key"] = "status", ["value"] = "ok" } };
        await new SetPayloadHandler().ExecuteAsync(ctx, node);
        Assert.Equal("ok", ctx.Get("status"));
    }

    [Fact]
    public async Task SetPayload_ResolvesPlaceholderInValue()
    {
        var ctx = Ctx.Make(new() { ["name"] = "Max" });
        var node = new FlowNode { Type = "set_payload", Config = new() { ["key"] = "greeting", ["value"] = "Hallo {name}" } };
        await new SetPayloadHandler().ExecuteAsync(ctx, node);
        Assert.Equal("Hallo Max", ctx.Get("greeting"));
    }

    [Fact]
    public async Task Function_ParsesJsonPayload_IncludingNestedAsRawJson()
    {
        var ctx = Ctx.Make();
        var node = new FlowNode
        {
            Type = "function",
            Config = new() { ["payload"] = """{ "host": "h", "n": 2, "obj": { "a": 1 } }""" },
        };
        await new FunctionHandler().ExecuteAsync(ctx, node);
        Assert.Equal("h", ctx.Get("host"));
        Assert.Equal("2", ctx.Get("n"));
        Assert.Contains("\"a\"", ctx.Get("obj")); // nested object kept as raw JSON
    }

    [Fact]
    public async Task Function_InvalidJson_Throws()
    {
        var ctx = Ctx.Make();
        var node = new FlowNode { Type = "function", Config = new() { ["payload"] = "{ not json" } };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new FunctionHandler().ExecuteAsync(ctx, node));
    }

    [Fact]
    public async Task Debug_ReportsTraceEntry_AndDoesNotBlockWhenNotPaused()
    {
        var rec = new TraceRecorder();
        var ctx = Ctx.Make(new() { ["status"] = "ok" }, rec);
        var node = new FlowNode { Id = "d1", Type = "debug", Config = new() { ["source"] = "payload", ["pause"] = "false" } };

        await new DebugHandler().ExecuteAsync(ctx, node); // must complete
        var entry = Assert.Single(rec.Entries);
        Assert.Equal("d1", entry.NodeId);
        Assert.Contains("status", entry.Message);
    }

    [Fact]
    public async Task LabelAndCaption_AreNoOps()
    {
        var ctx = Ctx.Make();
        await new LabelHandler().ExecuteAsync(ctx, new FlowNode { Type = "label" });
        await new CaptionHandler().ExecuteAsync(ctx, new FlowNode { Type = "caption" });
        Assert.Empty(ctx.Payload); // nothing changed
    }
}
