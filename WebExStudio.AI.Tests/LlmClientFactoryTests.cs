using WebExStudio.AI;
using Xunit;

namespace WebExStudio.AI.Tests;

public class LlmClientFactoryTests
{
    private static readonly HttpClient Http = new();

    [Theory]
    [InlineData("anthropic", "Anthropic/claude-sonnet-4-6")]
    [InlineData("openai", "OpenAI/gpt-4o")]
    [InlineData("ollama", "Ollama/llama3.1")]
    [InlineData("", "Anthropic/claude-sonnet-4-6")] // leer → Anthropic-Default
    public void Create_PicksProviderAndDefaultModel(string provider, string expectedName)
    {
        var client = LlmClientFactory.Create(new AiOptions { Provider = provider, ApiKey = "x" }, Http);
        Assert.Equal(expectedName, client.Name);
    }

    [Fact]
    public void Create_RespectsExplicitModel()
    {
        var client = LlmClientFactory.Create(
            new AiOptions { Provider = "anthropic", ApiKey = "x", Model = "claude-opus-4-7" }, Http);
        Assert.Equal("Anthropic/claude-opus-4-7", client.Name);
    }
}
