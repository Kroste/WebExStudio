using WebExStudio.Core.Localization;
using WebExStudio.Core.Models;
using Xunit;

namespace WebExStudio.Core.Tests;

public class NodeCatalogTests
{
    /// <summary>
    /// Jeder sichtbare Built-in-Node muss in JEDER nicht-deutschen Sprache vollständig abgedeckt sein
    /// (Name, Beschreibung, Beispiel, alle Property-Labels, alle Ausgangs-Labels). Deutsch kommt aus
    /// den Literalen (Fallback); fehlt ein Schlüssel in z. B. en/fr/ru, würde dort stillschweigend
    /// Deutsch erscheinen. Deutsch selbst hat absichtlich keine Node-Keys.
    /// </summary>
    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    [InlineData("ru")]
    public void Translation_CoversAllVisibleBuiltins(string lang)
    {
        var loc = Loc.Instance;
        var missing = new List<string>();

        foreach (var def in NodeCatalog.All.Where(d => !d.Hidden && !d.Type.Contains('.')))
        {
            void Need(string key) { if (!loc.Has(lang, key)) missing.Add(key); }

            Need($"Node_{def.Type}_Name");
            Need($"Node_{def.Type}_Desc");
            if (!string.IsNullOrWhiteSpace(def.Example)) Need($"Node_{def.Type}_Ex");
            foreach (var p in def.Properties) Need($"Node_{def.Type}_P_{p.Key}");
            for (int i = 0; i < def.OutputLabels.Length; i++) Need($"Node_{def.Type}_Out{i}");
        }

        // Kategorien ebenfalls.
        foreach (var cat in NodeCatalog.Categories)
            if (!loc.Has(lang, $"Cat_{cat}")) missing.Add($"Cat_{cat}");

        Assert.True(missing.Count == 0, $"Fehlende {lang}-Schlüssel: " + string.Join(", ", missing));
    }

    [Fact]
    public void Get_ReturnsKnown_AndNullForUnknown()
    {
        Assert.NotNull(NodeCatalog.Get("goto"));
        Assert.Null(NodeCatalog.Get("does_not_exist"));
    }

    [Fact]
    public void GetOrUnknown_FallsBack()
    {
        var def = NodeCatalog.GetOrUnknown("xyz");
        Assert.Equal("xyz", def.Type);
        Assert.Equal("Unbekannt", def.Category);
    }

    [Theory]
    [InlineData("if_then_else", 2, "then", "else")]
    [InlineData("foreach", 2, "Element", "fertig")]
    [InlineData("for_range", 2, "Schleife", "fertig")]
    [InlineData("get_links", 2, "je Link", "fertig")]
    public void ControlNodes_HaveTwoLabeledOutputs(string type, int ports, string p0, string p1)
    {
        var def = NodeCatalog.Get(type)!;
        Assert.Equal(ports, def.OutputPorts);
        Assert.Equal([p0, p1], def.OutputLabels);
    }

    [Fact]
    public void Annotations_HaveNoPorts()
    {
        var def = NodeCatalog.Get("note")!;
        Assert.Equal(0, def.InputPorts);
        Assert.Equal(0, def.OutputPorts);
    }

    [Fact]
    public void Quit_HasNoOutput()
    {
        Assert.Equal(0, NodeCatalog.Get("quit")!.OutputPorts);
    }

    [Fact]
    public void EveryNode_HasDescriptionAndExample()
    {
        // Versteckte/veraltete Alias-Nodes (z. B. caption/label) brauchen kein Beispiel.
        foreach (var def in NodeCatalog.All.Where(d => !d.Hidden))
        {
            Assert.False(string.IsNullOrWhiteSpace(def.Description), $"{def.Type} ohne Beschreibung");
            Assert.False(string.IsNullOrWhiteSpace(def.Example), $"{def.Type} ohne Beispiel");
        }
    }
}
