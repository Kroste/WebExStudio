using WebExStudio.AI.Providers;

namespace WebExStudio.AI;

/// <summary>Erzeugt den passenden <see cref="ILlmClient"/> aus den Optionen.</summary>
public static class LlmClientFactory
{
    /// <summary>Standardmodelle je Anbieter, falls in den Optionen keines gesetzt ist.</summary>
    public static string DefaultModel(string provider) => provider.ToLowerInvariant() switch
    {
        "openai" => "gpt-4o",
        "ollama" => "llama3.1",
        _ => "claude-sonnet-4-6",
    };

    public static ILlmClient Create(AiOptions o, HttpClient http)
    {
        var provider = string.IsNullOrWhiteSpace(o.Provider) ? "anthropic" : o.Provider.ToLowerInvariant();
        var model = string.IsNullOrWhiteSpace(o.Model) ? DefaultModel(provider) : o.Model;

        return provider switch
        {
            "openai" => new OpenAiClient(http, o.ApiKey, model, BaseOr(o, "https://api.openai.com")),
            "ollama" => new OllamaClient(http, model, BaseOr(o, "http://localhost:11434")),
            _ => new AnthropicClient(http, o.ApiKey, model, BaseOr(o, "https://api.anthropic.com")),
        };
    }

    private static string BaseOr(AiOptions o, string fallback) =>
        string.IsNullOrWhiteSpace(o.BaseUrl) ? fallback : o.BaseUrl.TrimEnd('/');
}
