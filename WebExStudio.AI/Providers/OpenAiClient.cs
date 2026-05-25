using System.Net.Http.Headers;

namespace WebExStudio.AI.Providers;

/// <summary>Anbindung an die OpenAI Chat-Completions API.</summary>
public sealed class OpenAiClient(HttpClient http, string apiKey, string model, string baseUrl) : ILlmClient
{
    public string Name => $"OpenAI/{model}";

    public async Task<string> ChatAsync(string systemPrompt, IReadOnlyList<ChatMessage> messages,
        bool jsonMode = false, CancellationToken ct = default)
    {
        var msgs = new List<object> { new { role = "system", content = systemPrompt } };
        msgs.AddRange(messages.Select(m =>
            new { role = m.Role == ChatRole.Assistant ? "assistant" : "user", content = m.Content }));

        var body = new
        {
            model,
            messages = msgs,
            response_format = jsonMode ? new { type = "json_object" } : null,
        };

        using var doc = await HttpJson.PostAsync(http, $"{baseUrl}/v1/chat/completions", body, req =>
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey), ct);

        var choice = doc.RootElement.GetProperty("choices").EnumerateArray().First();
        return choice.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
}
