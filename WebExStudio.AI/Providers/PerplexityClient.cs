using System.Net.Http.Headers;

namespace WebExStudio.AI.Providers;

/// <summary>
/// Anbindung an die Perplexity-API (OpenAI-kompatibles Chat-Format, aber unter
/// <c>/chat/completions</c> und ohne <c>response_format</c>). JSON wird per Prompt erzwungen.
/// </summary>
public sealed class PerplexityClient(HttpClient http, string apiKey, string model, string baseUrl) : ILlmClient
{
    public string Name => $"Perplexity/{model}";

    public async Task<string> ChatAsync(string systemPrompt, IReadOnlyList<ChatMessage> messages,
        bool jsonMode = false, CancellationToken ct = default)
    {
        var msgs = new List<object> { new { role = "system", content = systemPrompt } };
        msgs.AddRange(messages.Select(m =>
            new { role = m.Role == ChatRole.Assistant ? "assistant" : "user", content = m.Content }));

        var body = new { model, messages = msgs };

        using var doc = await HttpJson.PostAsync(http, $"{baseUrl}/chat/completions", body, req =>
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey), ct);

        var choice = doc.RootElement.GetProperty("choices").EnumerateArray().First();
        return choice.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
}
