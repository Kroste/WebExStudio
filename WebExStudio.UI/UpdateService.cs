using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NLog;

namespace WebExStudio.UI;

/// <summary>
/// Update-Check gegen GitHub Releases nach Kroste-Muster:
/// <list type="bullet">
///   <item>Aktuelle Version aus <c>AssemblyInformationalVersion</c> (MinVer-Metadaten wie <c>+sha</c>
///     werden vor dem Vergleich abgeschnitten).</item>
///   <item>Neueste Version über <c>GET api.github.com/repos/Kroste/&lt;Repo&gt;/releases/latest</c>
///     (Pflicht-Header <c>User-Agent</c>; ohne den antwortet die GitHub-API mit 403).</item>
///   <item>Vergleich über <see cref="System.Version"/> (nicht per Stringvergleich).</item>
///   <item>Proxy-aware: nutzt System-Proxy + <see cref="CredentialCache.DefaultCredentials"/>. Auf
///     Bazzite ein No-Op, hinter einem Firmen-Proxy (Kerberos/Negotiate) essenziell.</item>
///   <item>Ergebnis für die Programmlaufzeit gecacht (Rate-Limit 60/h/IP schont sich damit selbst).</item>
///   <item>Fehler werden nur geloggt — <b>nie als Fehlerdialog</b>. Offline oder Proxy-Probleme
///     dürfen den Nutzer nicht stören.</item>
/// </list>
/// Kein automatischer Self-Update-Austausch: bei verfügbarem Update öffnet der Nutzer die
/// Release-Seite im Browser (bewusste Zustimmung vor Installation).
/// </summary>
public static class UpdateService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private const string Owner = "Kroste";
    private const string Repo = "WebExStudio";
    private const string ApiUrl = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
    private const string ReleasesUrl = $"https://github.com/{Owner}/{Repo}/releases/latest";

    private static UpdateResult? _cached;

    public sealed record UpdateResult(
        Version Current,
        Version? Latest,
        string? LatestTag,
        string ReleaseUrl,
        bool HasUpdate,
        string? Error)
    {
        public static UpdateResult Failed(Version current, string error) =>
            new(current, null, null, ReleasesUrl, false, error);
    }

    /// <summary>Aktuelle Version dieser Assembly (Metadaten abgeschnitten). Ohne führendes v.</summary>
    public static Version CurrentVersion()
    {
        var raw = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "0.0.0";
        // "0.59.0+abcd" → "0.59.0"; "v1.2.3" → "1.2.3"
        var clean = raw.Split('+')[0].TrimStart('v', 'V');
        return TryParseVersion(clean, out var v) ? v : new Version(0, 0, 0);
    }

    /// <summary>URL der GitHub-Release-Seite (öffentlich, ohne Token).</summary>
    public static string ReleasePageUrl => ReleasesUrl;

    /// <summary>
    /// Prüft einmal pro Programmlaufzeit gegen die GitHub-API und liefert das gecachte Ergebnis.
    /// </summary>
    public static async Task<UpdateResult> CheckForUpdateAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        if (!forceRefresh && _cached is not null) return _cached;

        var current = CurrentVersion();
        try
        {
            using var handler = new HttpClientHandler
            {
                // Proxy-aware: System-Proxy + Default-Credentials (Kerberos/Negotiate am Firmenproxy).
                Proxy = WebRequest.DefaultWebProxy,
                DefaultProxyCredentials = CredentialCache.DefaultCredentials,
            };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"{Repo}/{current}");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            Log.Debug("Update-Check: GET {0}", ApiUrl);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var json = await http.GetStringAsync(ApiUrl, ct);
            sw.Stop();

            var release = JsonSerializer.Deserialize<ReleaseDto>(json);
            var tag = release?.TagName ?? "";
            if (!TryParseVersion(tag.TrimStart('v', 'V'), out var latest))
            {
                Log.Warn("Update-Check: unlesbarer Tag '{0}' ({1}ms)", tag, sw.ElapsedMilliseconds);
                _cached = UpdateResult.Failed(current, "invalid-tag");
                return _cached;
            }

            var hasUpdate = latest > current;
            var url = string.IsNullOrEmpty(release?.HtmlUrl) ? ReleasesUrl : release!.HtmlUrl!;
            Log.Info("Update-Check: aktuell {0}, neueste {1} ({2}ms, Update={3})",
                current, latest, sw.ElapsedMilliseconds, hasUpdate);
            _cached = new UpdateResult(current, latest, tag, url, hasUpdate, null);
            return _cached;
        }
        catch (Exception ex)
        {
            // Nur Warn — Update-Check-Fehler dürfen den Nutzer nicht stören.
            Log.Warn("Update-Check fehlgeschlagen: {0}", ex.Message);
            _cached = UpdateResult.Failed(current, ex.GetType().Name);
            return _cached;
        }
    }

    private static bool TryParseVersion(string s, out Version version)
    {
        // Auch nur „1.2" oder „1.2.3.4" akzeptieren.
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(s)) return false;
        // Entferne Suffixe wie "-rc1" / "-beta"
        var main = new string([.. s.TakeWhile(c => char.IsDigit(c) || c == '.')]);
        return Version.TryParse(main, out version!);
    }

    private sealed class ReleaseDto
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
    }
}
