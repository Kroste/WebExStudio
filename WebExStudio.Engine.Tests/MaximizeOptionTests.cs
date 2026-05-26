using WebExStudio.Core.Models;
using WebExStudio.Engine;
using Xunit;

namespace WebExStudio.Engine.Tests;

/// <summary>
/// Sichert die Logik fürs maximierte Browserfenster ab (ohne echten Browser):
/// --start-maximized nur bei sichtbarem Chromium, Viewport-Deaktivierung bei sichtbarem Fenster.
/// </summary>
public class MaximizeOptionTests
{
    [Fact]
    public void MaximizeArgs_Empty_WhenNotRequested()
    {
        var cfg = new RunConfig { Maximized = false, Headless = false, Browser = "chromium" };
        Assert.Empty(FlowExecutor.MaximizeArgs(cfg));
    }

    [Fact]
    public void MaximizeArgs_AddsFlag_ForVisibleChromium()
    {
        var cfg = new RunConfig { Maximized = true, Headless = false, Browser = "chromium" };
        Assert.Equal(["--start-maximized"], FlowExecutor.MaximizeArgs(cfg));
    }

    [Fact]
    public void MaximizeArgs_Empty_WhenHeadless()
    {
        var cfg = new RunConfig { Maximized = true, Headless = true, Browser = "chromium" };
        Assert.Empty(FlowExecutor.MaximizeArgs(cfg));
    }

    [Theory]
    [InlineData("firefox")]
    [InlineData("webkit")]
    public void MaximizeArgs_Empty_ForNonChromium(string browser)
    {
        var cfg = new RunConfig { Maximized = true, Headless = false, Browser = browser };
        Assert.Empty(FlowExecutor.MaximizeArgs(cfg));
    }

    [Fact]
    public void UseWindowViewport_True_OnlyForVisibleMaximized()
    {
        Assert.True(FlowExecutor.UseWindowViewport(new RunConfig { Maximized = true, Headless = false }));
        Assert.False(FlowExecutor.UseWindowViewport(new RunConfig { Maximized = true, Headless = true }));
        Assert.False(FlowExecutor.UseWindowViewport(new RunConfig { Maximized = false, Headless = false }));
    }
}
