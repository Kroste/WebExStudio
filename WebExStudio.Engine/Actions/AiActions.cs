using NLog;
using WebExStudio.Core.Logging;
using WebExStudio.Core.Models;

namespace WebExStudio.Engine.Actions;

/// <summary>
/// Schickt den Inhalt der aktuellen Seite (Text oder HTML) zusammen mit einer Anweisung an die
/// konfigurierte KI und legt die Antwort im Payload ab (z. B. „Extrahiere alle Preise als JSON").
/// </summary>
public sealed class AiQueryHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "ai_query";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var prompt = ctx.Fmt(node.Get("prompt"));
        if (string.IsNullOrWhiteSpace(prompt))
        {
            Log.Warn("ai_query: keine Anweisung (prompt) angegeben");
            return;
        }
        if (ctx.AiComplete is null)
        {
            Log.Warn("ai_query: KI nicht konfiguriert — bitte in den Einstellungen → KI einrichten");
            return;
        }

        var ctxKey = node.Get("ctx_key", "ai_result");
        var selector = ctx.Fmt(node.Get("selector"));
        var source = node.Get("source", "text").ToLowerInvariant();
        var maxChars = int.TryParse(node.Get("max_chars", "12000"), out var mc) && mc > 0 ? mc : 12000;
        var json = node.GetBool("json");

        var content = Truncate(await GrabContentAsync(ctx, selector, source), maxChars);

        var system = json
            ? "Du bist ein Assistent, der Informationen aus Webseiten extrahiert. Antworte ausschließlich mit gültigem JSON, ohne Erklärungen oder Markdown."
            : "Du bist ein Assistent, der Informationen aus Webseiten extrahiert. Antworte knapp und nur mit dem angeforderten Ergebnis.";
        var user = $"{prompt}\n\n--- Seiteninhalt ---\n{content}";

        // KI-Anfrage/-Antwort protokollieren (maskiert, da Seiteninhalt Geheimnisse enthalten kann).
        Log.Info("ai_query: Anfrage an KI ({0} Zeichen Inhalt, json={1})", content.Length, json);
        Log.Debug("ai_query Anfrage: {0}", SecretMasker.Mask(user));

        var answer = await ctx.AiComplete(system, user, json, ctx.CancellationToken) ?? string.Empty;
        Log.Debug("ai_query Antwort: {0}", SecretMasker.Mask(answer));

        ctx.Set(ctxKey, answer);
    }

    private static async Task<string> GrabContentAsync(ExecutionContext ctx, string selector, string source)
    {
        var html = source == "html";
        if (!string.IsNullOrEmpty(selector))
        {
            var loc = ctx.Page.Locator(selector).First;
            return html
                ? await loc.EvaluateAsync<string>("el => el.outerHTML") ?? string.Empty
                : await loc.InnerTextAsync(new() { Timeout = ctx.Config.TimeoutMs });
        }
        return html ? await ctx.Page.ContentAsync() : await ctx.Page.InnerTextAsync("body");
    }

    /// <summary>Kürzt den Seiteninhalt, um Token/Kosten zu begrenzen.</summary>
    public static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "\n…[gekürzt]";
}
