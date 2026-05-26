using WebExStudio.Core.Models;
using WebExStudio.Engine;
using WebExStudio.Engine.Actions;
using Xunit;

namespace WebExStudio.Engine.Tests;

/// <summary>Pfad-Auflösung für die Sitzungs-Persistenz (ohne Browser/Dateizugriff).</summary>
public class SessionPathTests
{
    [Fact]
    public void ResolveSessionPath_Null_WhenDisabled() =>
        Assert.Null(FlowExecutor.ResolveSessionPath(new RunConfig { SessionPersist = false }));

    [Fact]
    public void ResolveSessionPath_DefaultsToProjectSessionJson()
    {
        var cfg = new RunConfig { SessionPersist = true, ProjectDir = "/proj", SessionFile = "" };
        Assert.Equal(Path.Combine("/proj", "session.json"), FlowExecutor.ResolveSessionPath(cfg));
    }

    [Fact]
    public void ResolveSessionPath_RelativeFile_IsRootedAtProjectDir()
    {
        var cfg = new RunConfig { SessionPersist = true, ProjectDir = "/proj", SessionFile = "sub/s.json" };
        Assert.Equal(Path.Combine("/proj", "sub/s.json"), FlowExecutor.ResolveSessionPath(cfg));
    }

    [Fact]
    public void ResolveSessionPath_AbsoluteFile_IsKept()
    {
        var abs = Path.Combine(Path.GetTempPath(), "x.json");
        var cfg = new RunConfig { SessionPersist = true, ProjectDir = "/proj", SessionFile = abs };
        Assert.Equal(abs, FlowExecutor.ResolveSessionPath(cfg));
    }

    [Fact]
    public void SaveSession_ResolveSavePath_PrefersNodePathThenConfigThenDefault()
    {
        // Node-Pfad hat Vorrang
        Assert.Equal(Path.Combine("/proj", "n.json"),
            SaveSessionHandler.ResolveSavePath("/proj", "/proj/cfg.json", "n.json"));
        // sonst Einstellungs-Pfad
        Assert.Equal("/proj/cfg.json",
            SaveSessionHandler.ResolveSavePath("/proj", "/proj/cfg.json", ""));
        // sonst Default
        Assert.Equal(Path.Combine("/proj", "session.json"),
            SaveSessionHandler.ResolveSavePath("/proj", "", null));
    }
}
