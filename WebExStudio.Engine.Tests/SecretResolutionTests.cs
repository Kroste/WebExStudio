using WebExStudio.Core.Models;
using WebExStudio.Engine.Actions;
using EngineContext = WebExStudio.Engine.ExecutionContext;
using Xunit;

namespace WebExStudio.Engine.Tests;

/// <summary>Auflösung & Maskierung von {secret[name].field} — inkl. der Anti-Leak-Regel,
/// dass Secrets nie über Fmt/Payload aufgelöst werden.</summary>
public class SecretResolutionTests
{
    private static EngineContext Ctx(Func<string, string, string?> lookup, Dictionary<string, string>? payload = null) =>
        new(page: null!, new TargetConfig { Name = "t" }, new RunConfig(), projectDir: "", payload: payload)
        { SecretLookup = lookup };

    [Fact]
    public void FmtSecret_ResolvesAndMaskTracks()
    {
        var ctx = Ctx((n, f) => n == "F95" && f == "password" ? "p@ss" : null);
        var resolved = ctx.FmtSecret("pw={secret[F95].password}");
        Assert.Equal("pw=p@ss", resolved);
        Assert.Equal("pw=***", ctx.MaskSecrets(resolved)); // im Log maskiert
    }

    [Fact]
    public void ResolveSecrets_Throws_WhenLockedOrMissing()
    {
        Assert.Throws<InvalidOperationException>(() => Ctx((_, _) => null).ResolveSecrets("{secret[X].user}"));
        // kein Lookup verdrahtet (Tresor nicht verfügbar)
        var noLookup = new EngineContext(page: null!, new TargetConfig { Name = "t" }, new RunConfig(), "");
        Assert.Throws<InvalidOperationException>(() => noLookup.ResolveSecrets("{secret[X].user}"));
    }

    [Fact]
    public void Fmt_DoesNotResolveSecrets()
    {
        var ctx = Ctx((_, _) => "LEAK");
        Assert.Equal("{secret[A].user}", ctx.Fmt("{secret[A].user}")); // bleibt Platzhalter
    }

    [Fact]
    public async Task SetPayload_KeepsPlaceholder_NeverResolvesSecret()
    {
        var ctx = Ctx((_, _) => "LEAK");
        var node = new FlowNode { Type = "set_payload", Config = new() { ["key"] = "k", ["value"] = "{secret[A].user}" } };
        await new SetPayloadHandler().ExecuteAsync(ctx, node);
        Assert.Equal("{secret[A].user}", ctx.Get("k")); // Wert landet NIE im Payload
    }

    [Fact]
    public void FmtSecret_NoSecretRef_PassesThrough()
    {
        var ctx = Ctx((_, _) => "x", new() { ["host"] = "example.com" });
        Assert.Equal("example.com/login", ctx.FmtSecret("{payload.host}/login"));
    }
}
