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

    [Theory]
    [InlineData("kurz", 10, "kurz")]
    [InlineData("123456789", 5, "12345\n…[gekürzt]")]
    public void AiQuery_Truncate_LimitsLength(string input, int max, string expected) =>
        Assert.Equal(expected, AiQueryHandler.Truncate(input, max));

    [Fact]
    public async Task AiQuery_NoClient_IsNoOp()
    {
        // Ohne konfigurierte KI (AiComplete == null) darf der Node nichts schreiben und nicht werfen.
        var ctx = Ctx.Make();
        var node = new FlowNode { Type = "ai_query", Config = new() { ["prompt"] = "extrahiere", ["ctx_key"] = "r" } };
        await new AiQueryHandler().ExecuteAsync(ctx, node);
        Assert.Equal("", ctx.Get("r"));
    }

    [Fact]
    public void PageFunction_ObjectToPairs_MapsReturnedObjectToPayload()
    {
        using var obj = System.Text.Json.JsonDocument.Parse("""{ "anzahl": 5, "titel": "Start", "ok": true }""");
        var pairs = PageFunctionHandler.ObjectToPairs(obj.RootElement).ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal("5", pairs["anzahl"]);
        Assert.Equal("Start", pairs["titel"]); // String roh, ohne Anführungszeichen
        Assert.Equal("true", pairs["ok"]);
    }

    [Theory]
    [InlineData("42")]
    [InlineData("\"text\"")]
    [InlineData("null")]
    public void PageFunction_ObjectToPairs_EmptyForNonObject(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Empty(PageFunctionHandler.ObjectToPairs(doc.RootElement));
    }

    [Fact]
    public void PageFunction_ToStringValue_CoercesReturnKinds()
    {
        using var str = System.Text.Json.JsonDocument.Parse("\"hallo\"");
        using var num = System.Text.Json.JsonDocument.Parse("42");
        using var bln = System.Text.Json.JsonDocument.Parse("true");
        using var nul = System.Text.Json.JsonDocument.Parse("null");
        Assert.Equal("hallo", PageFunctionHandler.ToStringValue(str.RootElement)); // String roh, ohne Anführungszeichen
        Assert.Equal("42", PageFunctionHandler.ToStringValue(num.RootElement));
        Assert.Equal("true", PageFunctionHandler.ToStringValue(bln.RootElement));
        Assert.Equal("", PageFunctionHandler.ToStringValue(nul.RootElement));
        Assert.Equal("", PageFunctionHandler.ToStringValue(null));
    }

    [Fact]
    public async Task Assert_PayloadContains_PassesWhenPresent()
    {
        var ctx = Ctx.Make(new() { ["visited"] = "/a/\n/b/" });
        var node = new FlowNode
        {
            Type = "assert",
            Config = new() { ["condition"] = "payload_contains", ["selector"] = "visited", ["value"] = "/b/" },
        };
        // erfüllt → wirft nicht
        await new AssertHandler().ExecuteAsync(ctx, node);
    }

    [Fact]
    public async Task Assert_PayloadContains_ThrowsWhenMissing()
    {
        var ctx = Ctx.Make(new() { ["visited"] = "/a/" });
        var node = new FlowNode
        {
            Type = "assert",
            Config = new() { ["condition"] = "payload_contains", ["selector"] = "visited", ["value"] = "/b/", ["message"] = "Link fehlt" },
        };
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new AssertHandler().ExecuteAsync(ctx, node));
        Assert.Contains("Link fehlt", ex.Message);
    }

    [Fact]
    public async Task Assert_Negate_ThrowsWhenConditionTrue()
    {
        var ctx = Ctx.Make(new() { ["v"] = "treffer" });
        var node = new FlowNode
        {
            Type = "assert",
            Config = new() { ["condition"] = "payload_contains", ["selector"] = "v", ["value"] = "treffer", ["negate"] = "true" },
        };
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new AssertHandler().ExecuteAsync(ctx, node));
    }

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
