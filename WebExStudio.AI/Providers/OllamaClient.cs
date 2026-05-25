namespace WebExStudio.AI.Providers;

/// <summary>Anbindung an eine lokale Ollama-Instanz (kein API-Schlüssel nötig).</summary>
public sealed class OllamaClient(HttpClient http, string model, string baseUrl) : ILlmClient
{
    public string Name => $"Ollama/{model}";

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var body = new
        {
            model,
            stream = false,
            format = "json",
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
        };

        using var doc = await HttpJson.PostAsync(http, $"{baseUrl}/api/chat", body, _ => { }, ct);
        return doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
}
