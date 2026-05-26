using WebExStudio.Cli;
using Xunit;

namespace WebExStudio.Cli.Tests;

/// <summary>Argument-Parsing der CLI (reine Logik, ohne Browser/IO).</summary>
public class OptionsParseTests
{
    [Fact]
    public void NoArgs_DefaultsToHelp()
    {
        var o = Options.Parse([]);
        Assert.Equal("help", o.Command);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    public void HelpFlag_SetsHelp(string flag)
    {
        Assert.Equal("help", Options.Parse([flag]).Command);
    }

    [Theory]
    [InlineData("run")]
    [InlineData("validate")]
    [InlineData("secrets")]
    public void Command_IsFirstNonFlagToken(string cmd)
    {
        var o = Options.Parse([cmd, "-f", "flow.json"]);
        Assert.Equal(cmd, o.Command);
        Assert.Equal("flow.json", o.FlowPath);
    }

    [Fact]
    public void Run_ParsesAllOptions()
    {
        var o = Options.Parse([
            "run", "--flow", "f.json", "-c", "geheim",
            "--headful", "--browser", "firefox", "--timeout", "5000",
            "--download-dir", "/tmp/dl", "--out", "report.json",
        ]);

        Assert.Equal("run", o.Command);
        Assert.Equal("f.json", o.FlowPath);
        Assert.Equal("geheim", o.Password);
        Assert.True(o.Headful);
        Assert.Equal("firefox", o.Browser);
        Assert.Equal(5000, o.TimeoutMs);
        Assert.Equal("/tmp/dl", o.DownloadDir);
        Assert.Equal("report.json", o.OutPath);
    }

    [Fact]
    public void Defaults_AreConservative()
    {
        var o = Options.Parse(["run", "-f", "f.json"]);
        Assert.False(o.Headful);          // standardmäßig headless
        Assert.Null(o.Browser);
        Assert.Null(o.TimeoutMs);
        Assert.Null(o.OutPath);
        Assert.Empty(o.Vars);
    }

    [Fact]
    public void Var_CollectsMultiple_AndKeepsEqualsInValue()
    {
        var o = Options.Parse([
            "run", "-f", "f.json",
            "--var", "user=lars",
            "--var", "url=https://x?a=b&c=d",
        ]);

        Assert.Equal("lars", o.Vars["user"]);
        Assert.Equal("https://x?a=b&c=d", o.Vars["url"]); // '=' im Wert bleibt erhalten
    }

    [Fact]
    public void Timeout_NonNumeric_Throws()
    {
        Assert.Throws<ArgumentException>(() => Options.Parse(["run", "-f", "f.json", "--timeout", "abc"]));
    }

    [Fact]
    public void Var_WithoutEquals_Throws()
    {
        Assert.Throws<ArgumentException>(() => Options.Parse(["run", "--var", "kaputt"]));
    }

    [Fact]
    public void UnknownOption_Throws()
    {
        Assert.Throws<ArgumentException>(() => Options.Parse(["run", "--nope"]));
    }

    [Fact]
    public void MissingValue_Throws()
    {
        Assert.Throws<ArgumentException>(() => Options.Parse(["run", "-f"]));
    }
}
