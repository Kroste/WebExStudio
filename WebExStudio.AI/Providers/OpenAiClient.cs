using System.Net.Http.Headers;

namespace WebExStudio.AI.Providers;

/// <summary>Anbindung an die OpenAI Chat-Completions API.</summary>
public sealed class OpenAiClient(HttpClient http, string apiKey, string model, string baseUrl) : ILlmClient
{
    public string Name => $"OpenAI/{model}";

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var body = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
            response_format = new { type = "json_object" },
        };

        using var doc = await HttpJson.PostAsync(http, $"{baseUrl}/v1/chat/completions", body, req =>
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey), ct);

        var choice = doc.RootElement.GetProperty("choices").EnumerateArray().First();
        return choice.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
}
