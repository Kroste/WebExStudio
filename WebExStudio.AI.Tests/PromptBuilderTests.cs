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
}
