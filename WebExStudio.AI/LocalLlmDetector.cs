using System.Text.Json;

namespace WebExStudio.AI;

/// <summary>
/// Erkennt lokal laufende LLM-Server (z. B. via Pinokio): Ollama (native API) und
/// OpenAI-kompatible Server (LM Studio, llama.cpp, Jan). Best-Effort über bekannte Ports.
/// </summary>
public static class LocalLlmDetector
{
    /// <summary>Gefundener lokaler Server: Anbieter-Kennung, Basis-URL und installierte Modelle.</summary>
    public sealed record Result(string Provider, string BaseUrl, IReadOnlyList<string> Models);

    private static readonly string[] OllamaBases = ["http://localhost:11434", "http://127.0.0.1:11434"];
    private static readonly string[] OpenAiBases =
        ["http://localhost:1234", "http://localhost:8080", "http://localhost:1337"]; // LM Studio / llama.cpp / Jan

    /// <summary>Probiert die bekannten lokalen Endpunkte und liefert den ersten Treffer (oder null).</summary>
    public static async Task<Result?> DetectAsync(HttpClient http, CancellationToken ct = default)
    {
        foreach (var b in OllamaBases)
            if (await TryGetAsync(http, $"{b}/api/tags", ParseOllamaTags, ct) is { } m)
                return new Result("ollama", b, m);

        foreach (var b in OpenAiBases)
            if (await TryGetAsync(http, $"{b}/v1/models", ParseOpenAiModels, ct) is { } m)
                return new Result("openai", b, m);

        return null;
    }

    private static async Task<IReadOnlyList<string>?> TryGetAsync(
        HttpClient http, string url, Func<string, IReadOnlyList<string>?> parse, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            using var resp = await http.GetAsync(url, cts.Token);
            if (!resp.IsSuccessStatusCode) return null;
            return parse(await resp.Content.ReadAsStringAsync(cts.Token));
        }
        catch
        {
            return null; // nicht erreichbar / kein passender Server
        }
    }

    /// <summary>Parst die Ollama-Antwort von <c>/api/tags</c>. Null, wenn es keine Ollama-Antwort ist.</summary>
    public static IReadOnlyList<string>? ParseOllamaTags(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("models", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return null;
            var list = new List<string>();
            foreach (var m in arr.EnumerateArray())
                if (m.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                    list.Add(n.GetString()!);
            return list;
        }
        catch { return null; }
    }

    /// <summary>Parst die OpenAI-kompatible Antwort von <c>/v1/models</c>. Null, wenn unpassend.</summary>
    public static IReadOnlyList<string>? ParseOpenAiModels(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return null;
            var list = new List<string>();
            foreach (var m in arr.EnumerateArray())
                if (m.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                    list.Add(id.GetString()!);
            return list;
        }
        catch { return null; }
    }
}
