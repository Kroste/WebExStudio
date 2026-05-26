using System.Text.Json;
using Microsoft.Playwright;
using NLog;
using WebExStudio.Core.Models;

namespace WebExStudio.Engine.Actions;

/// <summary>
/// „If/Else" für Sitzungen: Existiert eine (nicht zu alte) Sitzungsdatei, werden deren Cookies in den
/// laufenden Browser geladen und der Ausgang 0 ('geladen') genommen — sonst Ausgang 1 ('keine Sitzung').
/// So kann man bei vorhandener Sitzung direkt zur Seite navigieren, sonst den Login ausführen.
/// Hinweis: localStorage wird nicht wiederhergestellt (für cookie-basierte Logins gedacht).
/// </summary>
public sealed class UseSessionHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "use_session";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var path = SaveSessionHandler.ResolveSavePath(ctx.ProjectDir, ctx.Config.SessionFile, ctx.Fmt(node.Get("path")));
        var maxAgeHours = int.TryParse(node.Get("max_age_hours", "0"), out var h) ? h : 0;

        if (!SessionFileUsable(path, maxAgeHours, out var reason))
        {
            Log.Info("use_session: keine verwendbare Sitzung ({0}) → Ausgang 'keine Sitzung'", reason);
            await ctx.FollowOutput(node, 1);
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path, ctx.CancellationToken));
            var cookies = ParseCookies(doc.RootElement, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if (cookies.Count == 0)
            {
                Log.Info("use_session: Sitzungsdatei ohne gültige Cookies → Ausgang 'keine Sitzung'");
                await ctx.FollowOutput(node, 1);
                return;
            }
            await ctx.Page.Context.AddCookiesAsync(cookies);
            Log.Info("use_session: {0} Cookies aus {1} geladen → Ausgang 'geladen'", cookies.Count, path);
            await ctx.FollowOutput(node, 0);
        }
        catch (Exception ex)
        {
            Log.Warn("use_session: Sitzung konnte nicht geladen werden ({0}) → Ausgang 'keine Sitzung'", ex.Message);
            await ctx.FollowOutput(node, 1);
        }
    }

    /// <summary>Existiert die Sitzungsdatei und ist sie (falls max_age_hours&gt;0) nicht zu alt?</summary>
    public static bool SessionFileUsable(string path, int maxAgeHours, out string reason)
    {
        if (!File.Exists(path)) { reason = "Datei fehlt"; return false; }
        if (maxAgeHours > 0 && DateTime.Now - File.GetLastWriteTime(path) > TimeSpan.FromHours(maxAgeHours))
        {
            reason = $"älter als {maxAgeHours}h";
            return false;
        }
        reason = "ok";
        return true;
    }

    /// <summary>Parst die Cookies aus einem Playwright-storageState-JSON; überspringt abgelaufene.</summary>
    public static List<Cookie> ParseCookies(JsonElement root, long nowUnix)
    {
        var result = new List<Cookie>();
        if (!root.TryGetProperty("cookies", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var c in arr.EnumerateArray())
        {
            var name = Str(c, "name");
            if (string.IsNullOrEmpty(name)) continue;
            var cookie = new Cookie { Name = name, Value = Str(c, "value") };
            if (c.TryGetProperty("domain", out var d) && d.ValueKind == JsonValueKind.String) cookie.Domain = d.GetString();
            if (c.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String) cookie.Path = p.GetString();
            if (c.TryGetProperty("expires", out var e) && e.ValueKind == JsonValueKind.Number)
            {
                var exp = e.GetDouble();
                if (exp > 0 && exp < nowUnix) continue; // abgelaufenes Cookie auslassen
                cookie.Expires = (float)exp;
            }
            if (c.TryGetProperty("httpOnly", out var ho) && ho.ValueKind is JsonValueKind.True or JsonValueKind.False)
                cookie.HttpOnly = ho.GetBoolean();
            if (c.TryGetProperty("secure", out var se) && se.ValueKind is JsonValueKind.True or JsonValueKind.False)
                cookie.Secure = se.GetBoolean();
            if (c.TryGetProperty("sameSite", out var ss) && ss.ValueKind == JsonValueKind.String)
                cookie.SameSite = ParseSameSite(ss.GetString());
            result.Add(cookie);
        }
        return result;
    }

    private static string Str(JsonElement o, string key) =>
        o.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static SameSiteAttribute? ParseSameSite(string? s) => s?.ToLowerInvariant() switch
    {
        "strict" => SameSiteAttribute.Strict,
        "lax" => SameSiteAttribute.Lax,
        "none" => SameSiteAttribute.None,
        _ => null,
    };
}
