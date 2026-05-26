using System.Net.Http;
using System.Text;
using NLog;
using WebExStudio.Core.Models;
using WebExStudio.Engine.Actions;
using WebExStudio.Engine.Plugins;
using ExecutionContext = WebExStudio.Engine.ExecutionContext; // CS0104 vs System.Threading.ExecutionContext

// Markiert die Ziel-API-Version, damit der Loader bei Inkompatibilität warnt.
[assembly: WebExStudioPlugin(PluginApi.Version)]

namespace HttpRequestPlugin;

/// <summary>Plugin: stellt den Node „HTTP-Anfrage" bereit (REST/Webhook außerhalb des Browsers).</summary>
public sealed class HttpRequestPlugin : INodePlugin
{
    public IEnumerable<NodePluginNode> CreateNodes() =>
    [
        new(Definition(), new HttpRequestHandler()),
    ];

    private static NodeDefinition Definition() => new()
    {
        Type = "http_request",
        DisplayName = "HTTP-Anfrage",
        Category = "Plugins",
        Description = "Sendet eine HTTP-Anfrage (REST-API/Webhook) – ohne Browser. Methode, Header "
            + "(eine pro Zeile als 'Name: Wert') und Body werden unterstützt. {secret[..]} ist in "
            + "URL, Headern und Body erlaubt (z. B. Authorization-Token) und wird erst beim Senden "
            + "aufgelöst. Antwort-Body landet in ctx_key, der Status-Code in status_key.",
        Color = "#0277BD", Icon = "🌐",
        Example = "method = POST, url = https://api.example.com/notify, "
            + "headers = Authorization: Bearer {secret[API].token}, body = {\"text\":\"fertig\"}  "
            + "→  {response}, {response_status}",
        Properties =
        [
            new() { Key = "url", Label = "URL", Kind = PropertyKind.Url, Required = true },
            new() { Key = "method", Label = "Methode (GET/POST/PUT/DELETE/PATCH)", Kind = PropertyKind.Text, DefaultValue = "GET" },
            new() { Key = "headers", Label = "Header (eine pro Zeile: Name: Wert)", Kind = PropertyKind.MultilineText,
                    Placeholder = "Authorization: Bearer {secret[API].token}\nContent-Type: application/json" },
            new() { Key = "body", Label = "Body (optional)", Kind = PropertyKind.MultilineText },
            new() { Key = "ctx_key", Label = "Antwort-Body → Payload-Schlüssel", Kind = PropertyKind.Text, DefaultValue = "response" },
            new() { Key = "status_key", Label = "Status-Code → Payload-Schlüssel", Kind = PropertyKind.Text, DefaultValue = "response_status" },
            new() { Key = "timeout_ms", Label = "Timeout (ms, leer = Standard)", Kind = PropertyKind.Number },
            new() { Key = "fail_on_error", Label = "Bei Status ≥ 400 fehlschlagen", Kind = PropertyKind.Boolean, DefaultValue = "false" },
        ],
    };
}

/// <summary>Verhalten des „HTTP-Anfrage"-Nodes.</summary>
public sealed class HttpRequestHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    // Eine geteilte Instanz (Socket-Wiederverwendung); das Timeout wird pro Anfrage per Token gesetzt.
    private static readonly HttpClient Http = new() { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

    public string Type => "http_request";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var url = ctx.FmtSecret(node.Get("url")); // {secret[..]} z. B. Token in der URL
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("http_request: URL fehlt.");

        var method = node.Get("method", "GET").Trim();
        if (string.IsNullOrEmpty(method)) method = "GET";
        var ctxKey = node.Get("ctx_key", "response");
        var statusKey = node.Get("status_key", "response_status");
        var failOnError = node.GetBool("fail_on_error");
        var timeoutMs = int.TryParse(node.Get("timeout_ms"), out var t) && t > 0 ? t : ctx.Config.TimeoutMs;

        using var req = new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()), url);

        var body = node.Get("body");
        if (!string.IsNullOrEmpty(body))
            req.Content = new StringContent(ctx.FmtSecret(body), Encoding.UTF8); // {secret[..]} im Body erlaubt

        foreach (var line in node.Get("headers").Split('\n'))
        {
            var raw = line.Trim();
            if (raw.Length == 0) continue;
            var idx = raw.IndexOf(':');
            if (idx <= 0) continue;
            var name = raw[..idx].Trim();
            var value = ctx.FmtSecret(raw[(idx + 1)..].Trim()); // {secret[..]} im Header-Wert erlaubt
            // Content-Header (z. B. Content-Type) gehören an den Body, Request-Header an die Anfrage.
            if (req.Content is not null && name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
            {
                req.Content.Headers.Remove(name);
                req.Content.Headers.TryAddWithoutValidation(name, value);
            }
            else
            {
                req.Headers.TryAddWithoutValidation(name, value);
            }
        }

        Log.Info("HTTP {0} {1}", method.ToUpperInvariant(), ctx.MaskSecrets(url));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.CancellationToken);
        cts.CancelAfter(timeoutMs);

        using var resp = await Http.SendAsync(req, cts.Token);
        var respBody = await resp.Content.ReadAsStringAsync(cts.Token);
        var status = (int)resp.StatusCode;

        ctx.Set(statusKey, status.ToString());
        ctx.Set(ctxKey, respBody);
        // Body NICHT loggen (kann Token/Geheimnisse enthalten) — nur Status + Größe.
        Log.Info("HTTP-Antwort {0} ({1} Bytes) → {{{2}}}, {{{3}}}", status, respBody.Length, ctxKey, statusKey);

        if (failOnError && !resp.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {status} {resp.ReasonPhrase} bei {ctx.MaskSecrets(url)}");
    }
}
