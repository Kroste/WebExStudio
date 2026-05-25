using Xunit;

namespace WebExStudio.Engine.Tests;

public class ExecutionContextTests
{
    [Fact]
    public void Fmt_Resolves_Plain_And_Payload_Placeholders()
    {
        var ctx = Ctx.Make(new() { ["host"] = "example.com" });
        Assert.Equal("https://example.com/x", ctx.Fmt("https://{host}/x"));
        Assert.Equal("example.com", ctx.Fmt("{payload.host}"));
        Assert.Equal("", ctx.Fmt(null));
        Assert.Equal("kein {fehlt}", ctx.Fmt("kein {fehlt}")); // unknown placeholder kept
    }

    [Fact]
    public void GetSet_OperateOnPayload()
    {
        var ctx = Ctx.Make();
        Assert.Equal("fallback", ctx.Get("x", "fallback"));
        ctx.Set("x", "1");
        Assert.Equal("1", ctx.Get("x"));
        Assert.Equal("1", ctx.Payload["x"]);
    }

    [Fact]
    public void CreateChild_CopiesPayload_AndMergesExtra_AndIsolated()
    {
        var parent = Ctx.Make(new() { ["host"] = "H" });
        var child = parent.CreateChild(new() { ["item"] = "a" });

        Assert.Equal("H", child.Get("host"));   // inherited
        Assert.Equal("a", child.Get("item"));   // extra

        child.Set("host", "X");                  // mutate child
        Assert.Equal("H", parent.Get("host"));   // parent unaffected (copy)
    }
}
