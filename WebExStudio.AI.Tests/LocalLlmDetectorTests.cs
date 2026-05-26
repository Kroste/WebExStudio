using WebExStudio.AI;
using Xunit;

namespace WebExStudio.AI.Tests;

public class LocalLlmDetectorTests
{
    [Fact]
    public void ParseOllamaTags_ReturnsModelNames()
    {
        var json = """{ "models": [ { "name": "llama3.1:latest" }, { "name": "qwen2.5:7b" } ] }""";
        var models = LocalLlmDetector.ParseOllamaTags(json);
        Assert.Equal(["llama3.1:latest", "qwen2.5:7b"], models);
    }

    [Fact]
    public void ParseOllamaTags_EmptyWhenNoModelsInstalled()
    {
        Assert.Empty(LocalLlmDetector.ParseOllamaTags("""{ "models": [] }""")!);
    }

    [Theory]
    [InlineData("""{ "data": [] }""")]      // OpenAI-Form ohne models, aber kein Ollama
    [InlineData("not json")]
    public void ParseOllamaTags_NullWhenNotOllama(string json) =>
        Assert.Null(LocalLlmDetector.ParseOllamaTags(json));

    [Fact]
    public void ParseOpenAiModels_ReturnsIds()
    {
        var json = """{ "object": "list", "data": [ { "id": "lmstudio-model" }, { "id": "gpt-oss" } ] }""";
        var models = LocalLlmDetector.ParseOpenAiModels(json);
        Assert.Equal(["lmstudio-model", "gpt-oss"], models);
    }

    [Theory]
    [InlineData("""{ "models": [] }""")]     // Ollama-Form, kein OpenAI
    [InlineData("kaputt")]
    public void ParseOpenAiModels_NullWhenNotOpenAi(string json) =>
        Assert.Null(LocalLlmDetector.ParseOpenAiModels(json));
}
