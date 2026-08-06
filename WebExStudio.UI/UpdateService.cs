using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using NLog;

namespace WebExStudio.UI;

/// <summary>
/// Update-Check UND echtes Self-Update gegen GitHub Releases nach Kroste-Muster:
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
/// Self-Update (<see cref="DownloadAndApplyAsync"/>): lädt das zur laufenden Plattform passende
/// Release-Asset, startet ein Installer-Skript, das auf das Prozessende wartet, austauscht und neu
/// startet — danach MUSS der Aufrufer die App beenden (<see cref="TerminateForUpdate"/>). Kein
/// Silent-Install: der Nutzer bestätigt vorher über den „Update installieren"-Button.
/// </summary>
public static class UpdateService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private const string Owner = "Kroste";
    private const string Repo = "WebExStudio";
    private const string ApiUrl = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
    private const string ReleasesUrl = $"https://github.com/{Owner}/{Repo}/releases/latest";

    private static UpdateResult? _cached;

    /// <summary>Art des Austauschs für die laufende Plattform.</summary>
    private enum UpdateKind { None, WindowsZip, LinuxTarGz, LinuxAppImage }

    public sealed record UpdateResult(
        Version Current,
        Version? Latest,
        string? LatestTag,
        string ReleaseUrl,
        bool HasUpdate,
        string? Error,
        string? AssetName = null,
        string? AssetUrl = null)
    {
        /// <summary>True, wenn ein Update da ist UND ein zur Plattform passendes Asset existiert.</summary>
        public bool CanSelfUpdate => HasUpdate && !string.IsNullOrEmpty(AssetUrl);

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
            using var handler = ProxyAwareHandler();
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"{Repo}/{current}");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            Log.Debug("Update-Check: GET {0}", ApiUrl);
            var sw = Stopwatch.StartNew();
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

            // Passendes GUI-Asset für die laufende Plattform aus dem assets-Array wählen.
            string? assetName = null, assetUrl = null;
            var (kind, suffix) = PlatformAsset();
            if (hasUpdate && kind != UpdateKind.None && release?.Assets is { } assets)
            {
                // GUI-Paket beginnt mit "WebExStudio-" (die CLI-Assets "webex-" bewusst ausschließen).
                var asset = assets.FirstOrDefault(a =>
                    a.Name is not null
                    && a.Name.StartsWith("WebExStudio-", StringComparison.OrdinalIgnoreCase)
                    && a.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
                assetName = asset?.Name;
                assetUrl = asset?.BrowserDownloadUrl;
            }

            Log.Info("Update-Check: aktuell {0}, neueste {1} ({2}ms, Update={3}, Asset={4})",
                current, latest, sw.ElapsedMilliseconds, hasUpdate, assetName ?? "—");
            _cached = new UpdateResult(current, latest, tag, url, hasUpdate, null, assetName, assetUrl);
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

    /// <summary>
    /// Lädt das passende Release-Asset (mit Fortschritt) und startet den plattformspezifischen
    /// Austausch-Prozess. Gibt <c>true</c> zurück, wenn der Installer läuft — dann MUSS der Aufrufer
    /// die App sofort per <see cref="TerminateForUpdate"/> beenden, sonst wartet der Installer ewig
    /// auf das Prozessende und die UI hängt bei „100 %".
    /// </summary>
    public static async Task<bool> DownloadAndApplyAsync(
        UpdateResult update, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (!update.CanSelfUpdate)
        {
            Log.Warn("Self-Update: kein passendes Asset für diese Plattform — Abbruch.");
            return false;
        }

        var (kind, _) = PlatformAsset();
        if (kind == UpdateKind.None) return false;

        var work = Path.Combine(Path.GetTempPath(), $"WebExStudio-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(work);
        var assetPath = Path.Combine(work, update.AssetName!);
        try
        {
            Log.Info("Self-Update: lade {0}", update.AssetName);
            await DownloadFileAsync(update.AssetUrl!, assetPath, progress, ct);

            var appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            var pid = Environment.ProcessId;

            switch (kind)
            {
                case UpdateKind.WindowsZip: ApplyWindowsZip(assetPath, work, appDir, pid); break;
                case UpdateKind.LinuxTarGz: ApplyLinuxTarGz(assetPath, appDir, pid); break;
                case UpdateKind.LinuxAppImage: ApplyLinuxAppImage(assetPath, pid); break;
            }

            Log.Info("Self-Update: Installer gestartet ({0}) — App wird jetzt beendet.", kind);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Self-Update fehlgeschlagen");
            try { Directory.Delete(work, recursive: true); } catch { /* Aufräumen ist best effort */ }
            return false;
        }
    }

    /// <summary>
    /// Beendet die App für den Austausch. Normalweg <see cref="Environment.Exit(int)"/> mit
    /// <see cref="Process.Kill()"/>-Fallback nach ~1,5 s (der Installer wartet nur auf das
    /// Verschwinden der PID — das Ende muss nicht sauber sein).
    /// </summary>
    public static void TerminateForUpdate()
    {
        var killer = new Thread(() =>
        {
            Thread.Sleep(1500);
            try { Process.GetCurrentProcess().Kill(); } catch { /* egal */ }
        })
        { IsBackground = true };
        killer.Start();

        Log.Info("Self-Update: beende App für den Austausch.");
        LogManager.Flush();
        Environment.Exit(0);
    }

    // ── intern ─────────────────────────────────────────────────────────────────

    private static HttpClientHandler ProxyAwareHandler() => new()
    {
        // Proxy-aware: System-Proxy + Default-Credentials (Kerberos/Negotiate am Firmenproxy).
        Proxy = WebRequest.DefaultWebProxy,
        DefaultProxyCredentials = CredentialCache.DefaultCredentials,
    };

    /// <summary>Art des Austauschs + Asset-Namens-Suffix für die laufende Plattform.</summary>
    private static (UpdateKind kind, string suffix) PlatformAsset()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return (UpdateKind.WindowsZip, "-win-x64.zip");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Läuft die App als AppImage, ersetzt sie sich selbst über $APPIMAGE.
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPIMAGE"))
                ? (UpdateKind.LinuxAppImage, "-x86_64.AppImage")
                : (UpdateKind.LinuxTarGz, "-linux-x64.tar.gz");
        }
        return (UpdateKind.None, "");
    }

    private static async Task DownloadFileAsync(string url, string dest, IProgress<double>? progress, CancellationToken ct)
    {
        using var handler = ProxyAwareHandler();
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"{Repo}/{CurrentVersion()}");

        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? -1L;

        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(dest);
        var buffer = new byte[81920];
        long readTotal = 0;
        int n;
        while ((n = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, n), ct);
            readTotal += n;
            if (total > 0) progress?.Report((double)readTotal / total);
        }
        progress?.Report(1.0);
    }

    /// <summary>Windows: ZIP daneben entpacken, Batch schreibt (nach Prozessende) per xcopy zurück und startet neu.</summary>
    private static void ApplyWindowsZip(string zipPath, string work, string appDir, int pid)
    {
        var newDir = Path.Combine(work, "new");
        ZipFile.ExtractToDirectory(zipPath, newDir);
        var exe = Path.Combine(appDir, "WebExStudio.exe");
        var log = Path.Combine(work, "update.log");
        var bat = Path.Combine(Path.GetTempPath(), $"WebExStudio-update-{Guid.NewGuid():N}.bat");

        // WICHTIG: Batch-Zeilen OHNE führende Einrückung (ein eingerücktes :label ist für cmd kein
        // gültiges Sprungziel). /H kopiert auch den .playwright-Ordner samt node.exe zuverlässig mit.
        var lines = new[]
        {
            "@echo off",
            $"powershell -NoProfile -Command \"Wait-Process -Id {pid} -ErrorAction SilentlyContinue\"",
            "timeout /t 1 /nobreak >nul",
            $"xcopy /E /H /I /Y \"{newDir}\\*\" \"{appDir}\" >>\"{log}\" 2>&1",
            $"start \"\" /D \"{appDir}\" \"{exe}\"",
            $"rmdir /S /Q \"{work}\"",
            "del \"%~f0\"",
        };
        File.WriteAllLines(bat, lines);

        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{bat}\"")
        {
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        });
    }

    /// <summary>Linux tar.gz: nach Prozessende ins App-Verzeichnis entpacken, Binary chmod +x, Neustart.</summary>
    private static void ApplyLinuxTarGz(string tarPath, string appDir, int pid)
    {
        var bin = Path.Combine(appDir, "WebExStudio");
        var script = string.Join('\n', new[]
        {
            "#!/usr/bin/env sh",
            $"LOG=\"{UpdateLogPath()}\"",
            $"while kill -0 {pid} 2>/dev/null; do sleep 0.2; done",
            "sleep 0.3",
            $"tar -xzf \"{tarPath}\" -C \"{appDir}\" >>\"$LOG\" 2>&1",
            $"chmod +x \"{bin}\"",
            $"(cd \"{appDir}\" && setsid \"{bin}\" >/dev/null 2>&1) &",
            $"rm -f \"{tarPath}\" \"$0\"",
            "",
        });
        StartShScript(script);
    }

    /// <summary>Linux AppImage: das laufende $APPIMAGE per cp -f (Inode bleibt) ersetzen und neu starten.</summary>
    private static void ApplyLinuxAppImage(string newImage, int pid)
    {
        var appImage = Environment.GetEnvironmentVariable("APPIMAGE");
        if (string.IsNullOrEmpty(appImage))
            throw new InvalidOperationException("APPIMAGE-Variable nicht gesetzt — kein AppImage-Selbstaustausch möglich.");

        // Log in ein SCHREIBBARES Verzeichnis — beim laufenden AppImage ist BaseDirectory read-only
        // (Squashfs-Mount); ein Log dorthin würde das Skript sofort abbrechen lassen.
        var script = string.Join('\n', new[]
        {
            "#!/usr/bin/env sh",
            $"LOG=\"{UpdateLogPath()}\"",
            $"while kill -0 {pid} 2>/dev/null; do sleep 0.2; done",
            "sleep 0.3",
            $"cp -f \"{newImage}\" \"{appImage}\" >>\"$LOG\" 2>&1",
            $"chmod +x \"{appImage}\"",
            $"setsid \"{appImage}\" >/dev/null 2>&1 &",
            $"rm -f \"{newImage}\" \"$0\"",
            "",
        });
        StartShScript(script);
    }

    private static void StartShScript(string script)
    {
        var sh = Path.Combine(Path.GetTempPath(), $"WebExStudio-update-{Guid.NewGuid():N}.sh");
        File.WriteAllText(sh, script);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(sh,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        Process.Start(new ProcessStartInfo("/bin/sh", $"\"{sh}\"") { UseShellExecute = false });
    }

    /// <summary>Schreibbarer Log-Pfad (XDG_STATE_HOME → ~/.local/state → /tmp) für die Installer-Skripte.</summary>
    private static string UpdateLogPath()
    {
        var state = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
        if (string.IsNullOrEmpty(state))
            state = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state");
        try
        {
            var dir = Path.Combine(state, "WebExStudio");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "update.log");
        }
        catch
        {
            return Path.Combine(Path.GetTempPath(), "webexstudio-update.log");
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
        [JsonPropertyName("assets")] public List<AssetDto>? Assets { get; set; }
    }

    private sealed class AssetDto
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
    }
}
