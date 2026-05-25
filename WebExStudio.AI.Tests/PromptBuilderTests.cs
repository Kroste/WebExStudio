using WebExStudio.AI;
using Xunit;

namespace WebExStudio.AI.Tests;

public class PromptBuilderTests
{
    [Fact]
    public void ChatSystemPrompt_IncludesCurrentFlow_WhenProvided()
    {
        var prompt = PromptBuilder.BuildChatSystemPrompt("""{ "version": 2, "marker": "XYZ123" }""");
        Assert.Contains("AKTUELLER FLOW", prompt);
        Assert.Contains("XYZ123", prompt);
    }

    [Fact]
    public void ChatSystemPrompt_OmitsFlowSection_WhenNull()
    {
        var prompt = PromptBuilder.BuildChatSystemPrompt();
        Assert.DoesNotContain("AKTUELLER FLOW", prompt);
    }

    [Fact]
    public void AllSystemPrompts_IncludeHints_WhenProvided()
    {
        const string hint = "- MARKER_HINT_XYZ";
        Assert.Contains("MARKER_HINT_XYZ", PromptBuilder.BuildSystemPrompt(hint));
        Assert.Contains("MARKER_HINT_XYZ", PromptBuilder.BuildChatSystemPrompt(null, hint));
        Assert.Contains("MARKER_HINT_XYZ", PromptBuilder.BuildExplainSystemPrompt(hint));
        Assert.Contains("MARKER_HINT_XYZ", PromptBuilder.BuildSuggestSystemPrompt(hint));
        Assert.Contains("BEKANNTE HINWEISE", PromptBuilder.BuildSuggestSystemPrompt(hint));
    }

    [Fact]
    public void SystemPrompt_OmitsHintsSection_WhenEmpty()
    {
        Assert.DoesNotContain("BEKANNTE HINWEISE", PromptBuilder.BuildSystemPrompt(null));
        Assert.DoesNotContain("BEKANNTE HINWEISE", PromptBuilder.BuildSystemPrompt("   "));
    }

    [Theory]
    [InlineData(false, "x", null)]
    [InlineData(true, "   ", null)]
    [InlineData(true, " hinweis ", "hinweis")]
    public void AiOptions_ActiveHints(bool send, string hints, string? expected)
    {
        var o = new AiOptions { SendHints = send, Hints = hints };
        Assert.Equal(expected, o.ActiveHints);
    }
}
