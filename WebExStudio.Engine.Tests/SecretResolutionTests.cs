using WebExStudio.Core.Models;
using WebExStudio.Engine.Actions;
using Xunit;
using EngineContext = WebExStudio.Engine.ExecutionContext;

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


    /// <summary>
    /// Kernregel gegen Exfiltration: Payload-Inhalt kommt über get_value direkt von der besuchten
    /// Seite. Wird er eingesetzt, darf er NICHT erneut nach {secret[..]} durchsucht werden — sonst
    /// tippt eine präparierte Seite sich das echte Passwort selbst ins Formular.
    /// </summary>
    [Fact]
    public void FmtSecret_LoestKeineSecretsAusEingesetztemPayload()
    {
        var ctx = Ctx((_, _) => "GEHEIM", new() { ["gescraped"] = "{secret[github].password}" });

        var ergebnis = ctx.FmtSecret("{payload.gescraped}");

        Assert.DoesNotContain("GEHEIM", ergebnis);
        Assert.Equal("{secret[github].password}", ergebnis); // bleibt wörtlich stehen
    }

    /// <summary>Gegenprobe: ein Verweis, der in der Node-Konfiguration SELBST steht, wird weiterhin
    /// aufgelöst — nur eingesetzter Fremdtext nicht.</summary>
    [Fact]
    public void FmtSecret_LoestVerweisAusDerVorlageWeiterhinAuf()
    {
        var ctx = Ctx((n, f) => n == "github" && f == "password" ? "GEHEIM" : null,
            new() { ["user"] = "lars" });

        Assert.Equal("lars:GEHEIM", ctx.FmtSecret("{payload.user}:{secret[github].password}"));
    }

    /// <summary>Kein Ketten-Einsetzen: ein Payload-Wert, der selbst wie ein Platzhalter aussieht,
    /// bleibt stehen. Vorher hing das Ergebnis an der Aufzählungsreihenfolge des Dictionary.</summary>
    [Fact]
    public void Fmt_SetztNichtInBereitsEingesetztemTextWeiterEin()
    {
        var ctx = Ctx((_, _) => null, new() { ["a"] = "{b}", ["b"] = "X" });
        Assert.Equal("{b}", ctx.Fmt("{a}"));
    }

    [Theory]
    [InlineData("{ \"host\": \"{payload.host}\" }", "{ \"host\": \"example.com\" }")] // JSON drumherum
    [InlineData("kein {fehlt}", "kein {fehlt}")]                                          // unbekannt bleibt
    [InlineData("offen {ohne Ende", "offen {ohne Ende")]                                  // keine schließende Klammer
    [InlineData("{PAYLOAD.HOST}", "example.com")]                                         // Groß-/Kleinschreibung egal
    [InlineData("{host}{host}", "example.comexample.com")]                                // mehrfach
    public void Fmt_RandfaelleBleibenStabil(string ein, string aus)
    {
        var ctx = Ctx((_, _) => null, new() { ["host"] = "example.com" });
        Assert.Equal(aus, ctx.Fmt(ein));
    }

    [Theory]
    [InlineData("{secret[A].user extra}")]  // Feld mit unerlaubtem Zeichen
    [InlineData("{secret[].user}")]         // leerer Name
    [InlineData("{secret[A]user}")]         // fehlender Punkt
    [InlineData("{secret[A].}")]            // leeres Feld
    public void FmtSecret_UngueltigeVerweiseBleibenStehen(string ein)
    {
        var ctx = Ctx((_, _) => "LEAK");
        Assert.Equal(ein, ctx.FmtSecret(ein));
    }

    [Fact]
    public void FmtSecret_NoSecretRef_PassesThrough()
    {
        var ctx = Ctx((_, _) => "x", new() { ["host"] = "example.com" });
        Assert.Equal("example.com/login", ctx.FmtSecret("{payload.host}/login"));
    }
}
