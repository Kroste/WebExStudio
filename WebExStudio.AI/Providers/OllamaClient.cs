namespace WebExStudio.AI.Providers;

/// <summary>Anbindung an eine lokale Ollama-Instanz (kein API-Schlüssel nötig).</summary>
public sealed class OllamaClient(HttpClient http, string model, string baseUrl) : ILlmClient
{
    public string Name => $"Ollama/{model}";

    public async Task<string> ChatAsync(string systemPrompt, IReadOnlyList<ChatMessage> messages,
        bool jsonMode = false, CancellationToken ct = default)
    {
        var msgs = new List<object> { new { role = "system", content = systemPrompt } };
        msgs.AddRange(messages.Select(m =>
            new { role = m.Role == ChatRole.Assistant ? "assistant" : "user", content = m.Content }));

        var body = new
        {
            model,
            stream = false,
            format = jsonMode ? "json" : null,
            messages = msgs,
        };

        using var doc = await HttpJson.PostAsync(http, $"{baseUrl}/api/chat", body, _ => { }, ct);
        return doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
}
