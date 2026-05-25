using System.Text;

namespace WebExStudio.AI.Providers;

/// <summary>Anbindung an die Anthropic Messages API (Claude).</summary>
public sealed class AnthropicClient(HttpClient http, string apiKey, string model, string baseUrl) : ILlmClient
{
    public string Name => $"Anthropic/{model}";

    public async Task<string> ChatAsync(string systemPrompt, IReadOnlyList<ChatMessage> messages,
        bool jsonMode = false, CancellationToken ct = default)
    {
        var body = new
        {
            model,
            max_tokens = 8192,
            system = systemPrompt,
            messages = messages
                .Select(m => new { role = m.Role == ChatRole.Assistant ? "assistant" : "user", content = m.Content })
                .ToArray(),
        };

        using var doc = await HttpJson.PostAsync(http, $"{baseUrl}/v1/messages", body, req =>
        {
            req.Headers.Add("x-api-key", apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");
        }, ct);

        // content: [ { "type": "text", "text": "…" }, … ]
        var sb = new StringBuilder();
        foreach (var block in doc.RootElement.GetProperty("content").EnumerateArray())
            if (block.TryGetProperty("text", out var text))
                sb.Append(text.GetString());
        return sb.ToString();
    }
}
