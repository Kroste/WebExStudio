using System.Text;

namespace WebExStudio.AI.Providers;

/// <summary>Anbindung an die Google Gemini API (Generative Language).</summary>
public sealed class GeminiClient(HttpClient http, string apiKey, string model, string baseUrl) : ILlmClient
{
    public string Name => $"Gemini/{model}";

    public async Task<string> ChatAsync(string systemPrompt, IReadOnlyList<ChatMessage> messages,
        bool jsonMode = false, CancellationToken ct = default)
    {
        // Gemini kennt die Rollen "user" und "model" (nicht "assistant").
        object? genConfig = jsonMode ? new { responseMimeType = "application/json" } : null;
        var body = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = messages.Select(m => new
            {
                role = m.Role == ChatRole.Assistant ? "model" : "user",
                parts = new[] { new { text = m.Content } },
            }).ToArray(),
            generationConfig = genConfig,
        };

        using var doc = await HttpJson.PostAsync(http,
            $"{baseUrl}/v1beta/models/{model}:generateContent", body,
            req => req.Headers.Add("x-goog-api-key", apiKey), ct);

        if (!doc.RootElement.TryGetProperty("candidates", out var candidates)
            || candidates.GetArrayLength() == 0)
            throw new HttpRequestException("Gemini: keine Antwort erhalten (evtl. durch Sicherheitsfilter blockiert).");

        var sb = new StringBuilder();
        foreach (var part in candidates[0].GetProperty("content").GetProperty("parts").EnumerateArray())
            if (part.TryGetProperty("text", out var t))
                sb.Append(t.GetString());
        return sb.ToString();
    }
}
