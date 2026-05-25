using WebExStudio.AI;
using Xunit;

namespace WebExStudio.AI.Tests;

public class NodeSuggesterTests
{
    [Fact]
    public void Parse_ValidSuggestion_Succeeds()
    {
        var raw = """
            { "type": "click", "label": "Login klicken",
              "config": { "selector": "#login" }, "reason": "Nach dem Laden folgt der Login." }
            """;
        var result = NodeSuggester.Parse(raw);

        Assert.True(result.Success);
        Assert.Equal("click", result.Suggestion!.Type);
        Assert.Equal("Login klicken", result.Suggestion.Label);
        Assert.Equal("#login", result.Suggestion.Config["selector"]);
    }

    [Fact]
    public void Parse_CoercesNonStringConfigValues()
    {
        // Modell liefert Zahl/Boolean statt String — der Converter wandelt um.
        var raw = """{ "type": "sleep", "config": { "seconds": 5 }, "reason": "warten" }""";
        var result = NodeSuggester.Parse(raw);

        Assert.True(result.Success);
        Assert.Equal("5", result.Suggestion!.Config["seconds"]);
    }

    [Fact]
    public void Parse_ExtractsFromProse()
    {
        var raw = "Klar:\n```json\n{ \"type\": \"goto\", \"config\": { \"url\": \"https://x\" }, \"reason\": \"Start\" }\n```";
        Assert.True(NodeSuggester.Parse(raw).Success);
    }

    [Fact]
    public void Parse_UnknownType_Fails()
    {
        var result = NodeSuggester.Parse("""{ "type": "teleport", "reason": "x" }""");
        Assert.False(result.Success);
        Assert.Contains("teleport", result.Error);
    }

    [Fact]
    public void Parse_NoType_Fails()
    {
        var result = NodeSuggester.Parse("""{ "reason": "x" }""");
        Assert.False(result.Success);
        Assert.Null(result.Suggestion);
    }

    [Fact]
    public void Parse_Garbage_Fails()
    {
        Assert.False(NodeSuggester.Parse("kein json hier").Success);
    }
}
