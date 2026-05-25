using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebExStudio.AI.Providers;

/// <summary>Kleiner Helfer für JSON-POSTs an die Anbieter-APIs.</summary>
internal static class HttpJson
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task<JsonDocument> PostAsync(
        HttpClient http, string url, object body,
        Action<HttpRequestMessage> configure, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, Opts), Encoding.UTF8, "application/json"),
        };
        configure(req);

        using var resp = await http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}: {Trim(text)}");

        return JsonDocument.Parse(text);
    }

    private static string Trim(string s) => s.Length > 500 ? s[..500] + "…" : s;
}
