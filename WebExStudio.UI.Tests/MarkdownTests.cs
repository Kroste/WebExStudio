using System.IO;
using WebExStudio.UI.Views;
using Xunit;

namespace WebExStudio.UI.Tests;

/// <summary>Reine Markdown-Hilfsfunktionen für das Hilfefenster (ohne Avalonia-Runtime).</summary>
public class MarkdownTests
{
    [Theory]
    [InlineData("siehe [Schnellstart](#schnellstart) hier", "siehe Schnellstart hier")]
    [InlineData("[Playwright](https://x) nutzen", "Playwright nutzen")]
    [InlineData("ohne Link", "ohne Link")]
    public void StripLinks_RemovesUrls(string input, string expected) =>
        Assert.Equal(expected, SimpleMarkdown.StripLinks(input));

    [Fact]
    public void SplitTableRow_TrimsCells()
    {
        Assert.Equal(["a", "b", "c"], SimpleMarkdown.SplitTableRow("| a | b | c |"));
        Assert.Equal(["x", "y"], SimpleMarkdown.SplitTableRow("|x|y|"));
    }

    [Theory]
    [InlineData("|---|---|", true)]
    [InlineData("| :--- | ---: |", true)]
    [InlineData("| a | b |", false)]
    [InlineData("kein tabellentext", false)]
    public void IsTableSeparator_Detects(string line, bool expected) =>
        Assert.Equal(expected, SimpleMarkdown.IsTableSeparator(line));

    [Fact]
    public void ParseInline_HandlesBoldAndCode()
    {
        var segs = SimpleMarkdown.ParseInline("normal **fett** und `code` Ende");
        Assert.Contains(segs, s => s.Text == "fett" && s.Bold && !s.Code);
        Assert.Contains(segs, s => s.Text == "code" && s.Code && !s.Bold);
        Assert.Contains(segs, s => s.Text == "normal " && !s.Bold && !s.Code);
    }

    [Fact]
    public void ParseInline_PlainText_SingleSegment()
    {
        var segs = SimpleMarkdown.ParseInline("nur text");
        Assert.Single(segs);
        Assert.Equal("nur text", segs[0].Text);
    }

    [Theory]
    [InlineData("WebExStudio.UI.README.md")]
    [InlineData("WebExStudio.UI.README.de.md")]
    [InlineData("WebExStudio.UI.README.fr.md")]
    [InlineData("WebExStudio.UI.README.ru.md")]
    public void EmbeddedReadme_IsAvailableForHelp(string resource)
    {
        // Sichert, dass die README(s) ins HAUPT-Assembly eingebettet bleiben (Quelle des Hilfefensters).
        // Wichtig bei den Sprachvarianten: WithCulture=false verhindert, dass sie als Satelliten-Assembly
        // (en/…) landen — sonst wäre der Stream hier null.
        using var s = typeof(HelpWindow).Assembly.GetManifestResourceStream(resource);
        Assert.NotNull(s);
        using var reader = new StreamReader(s!);
        Assert.Contains("# WebExStudio", reader.ReadToEnd());
    }
}
