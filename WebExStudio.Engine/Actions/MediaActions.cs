using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Playwright;
using NLog;
using WebExStudio.Core.Models;

namespace WebExStudio.Engine.Actions;

/// <summary>
/// Erkennt abspielende Medien einer Seite (eingebettete Videos/Audios, HLS/DASH-Streams), indem
/// der Netzwerkverkehr für ein Zeitfenster mitgeschnitten wird, schreibt die URLs ins Payload und
/// lädt sie: direkte Dateien (mp4/mp3 …) per HTTP, Segment-Streams (m3u8/mpd) via ffmpeg.
/// DRM-geschützte Streams sind technisch nicht ladbar.
/// </summary>
public sealed class DownloadStreamHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "download_stream";

    private static readonly string[] MediaExtensions =
        [".m3u8", ".mpd", ".mp4", ".webm", ".m4s", ".ts", ".mp3", ".m4a", ".aac", ".ogg", ".mov"];

    /// <summary>True, wenn URL oder Content-Type auf eine Mediendatei/-Stream hindeuten.</summary>
    public static bool IsMediaUrl(string url, string? contentType)
    {
        var c = (contentType ?? string.Empty).ToLowerInvariant();
        if (c.StartsWith("video/") || c.StartsWith("audio/")) return true;
        if (c.Contains("mpegurl") || c.Contains("dash+xml")) return true;
        var u = url.ToLowerInvariant();
        return MediaExtensions.Any(ext => u.Contains(ext));
    }

    /// <summary>True für HLS-/DASH-Manifeste (brauchen ffmpeg zum Zusammenfügen).</summary>
    public static bool IsManifest(string url)
    {
        var u = url.ToLowerInvariant();
        return u.Contains(".m3u8") || u.Contains(".mpd");
    }

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var waitMs = int.TryParse(node.Get("wait_ms", "8000"), out var w) ? w : 8000;
        var ctxKey = node.Get("ctx_key", "media_urls");
        var doDownload = node.GetBool("download", true);
        var ffmpeg = node.Get("ffmpeg_path", "ffmpeg");
        var filename = ctx.Fmt(node.Get("filename"));

        var found = new ConcurrentDictionary<string, byte>();
        void OnResponse(object? _, IResponse r)
        {
            try
            {
                r.Headers.TryGetValue("content-type", out var ctype);
                if (IsMediaUrl(r.Url, ctype)) found.TryAdd(r.Url, 0);
            }
            catch { /* Header evtl. nicht lesbar — ignorieren */ }
        }

        ctx.Page.Response += OnResponse;
        try
        {
            await CollectDomSourcesAsync(ctx, found);
            Log.Info("download_stream: lausche {0}ms auf Medien-Requests (Wiedergabe sollte laufen)…", waitMs);
            await Task.Delay(waitMs, ctx.CancellationToken);
        }
        finally
        {
            ctx.Page.Response -= OnResponse;
        }

        // Manifeste zuerst, dann direkte Dateien.
        var urls = found.Keys.OrderBy(u => IsManifest(u) ? 0 : 1).ToList();
        Log.Info("download_stream: {0} Medien-URL(s) erkannt", urls.Count);
        foreach (var u in urls) Log.Debug("  • {0}", u);
        ctx.Set(ctxKey, string.Join("\n", urls));

        if (!doDownload || urls.Count == 0) return;

        var downloadDir = string.IsNullOrEmpty(ctx.Config.DownloadDir)
            ? Path.Combine(ctx.ProjectDir, "downloads")
            : ctx.Config.DownloadDir;
        Directory.CreateDirectory(downloadDir);

        var manifest = urls.FirstOrDefault(IsManifest);
        if (manifest is not null)
            await DownloadViaFfmpegAsync(ctx, ffmpeg, manifest, downloadDir, filename);
        else
            await DownloadDirectAsync(ctx, urls[0], downloadDir, filename);
    }

    /// <summary>Liest bereits im DOM vorhandene Medienquellen (video/audio/source), ohne blob:-URLs.</summary>
    private static async Task CollectDomSourcesAsync(ExecutionContext ctx, ConcurrentDictionary<string, byte> found)
    {
        try
        {
            var srcs = await ctx.Page.EvaluateAsync<string[]>(
                "() => Array.from(document.querySelectorAll('video,audio,source')).map(e => e.currentSrc || e.src).filter(Boolean)");
            foreach (var s in srcs ?? [])
                if (!s.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
                    found.TryAdd(s, 0);
        }
        catch (Exception ex)
        {
            Log.Debug("download_stream: DOM-Quellen nicht lesbar: {0}", ex.Message);
        }
    }

    private static async Task DownloadDirectAsync(ExecutionContext ctx, string url, string dir, string filename)
    {
        if (string.IsNullOrEmpty(filename))
        {
            filename = Path.GetFileName(new Uri(url).LocalPath);
            if (string.IsNullOrEmpty(filename)) filename = $"media_{DateTime.Now:yyyyMMdd_HHmmss}";
        }
        var dest = Path.Combine(dir, filename);
        Log.Info("download_stream: direkter Download {0} → {1}", url, dest);
        using var http = new System.Net.Http.HttpClient();
        var bytes = await http.GetByteArrayAsync(url, ctx.CancellationToken);
        await File.WriteAllBytesAsync(dest, bytes, ctx.CancellationToken);
        ctx.Set("download_path", dest);
        Log.Debug("download_stream: {0} bytes gespeichert", bytes.Length);
    }

    private static async Task DownloadViaFfmpegAsync(ExecutionContext ctx, string ffmpeg, string manifestUrl, string dir, string filename)
    {
        var outName = string.IsNullOrEmpty(filename)
            ? $"stream_{DateTime.Now:yyyyMMdd_HHmmss}.mp4"
            : (Path.HasExtension(filename) ? filename : filename + ".mp4");
        var outPath = Path.Combine(dir, outName);

        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // Referer mitgeben — viele Stream-Server verlangen ihn.
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-headers");
        psi.ArgumentList.Add($"Referer: {ctx.Page.Url}\r\n");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(manifestUrl);
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("copy");
        psi.ArgumentList.Add(outPath);

        Log.Info("download_stream: ffmpeg lädt Stream {0} → {1}", manifestUrl, outPath);
        Process? proc;
        try
        {
            proc = Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log.Error("download_stream: ffmpeg nicht startbar ({0}). Bitte ffmpeg installieren oder 'ffmpeg_path' setzen.", ex.Message);
            return;
        }
        if (proc is null) { Log.Error("download_stream: ffmpeg konnte nicht gestartet werden."); return; }

        var stderr = await proc.StandardError.ReadToEndAsync(ctx.CancellationToken);
        await proc.WaitForExitAsync(ctx.CancellationToken);
        if (proc.ExitCode == 0)
        {
            ctx.Set("download_path", outPath);
            Log.Info("download_stream: Stream gespeichert: {0}", outPath);
        }
        else
        {
            var tail = string.Join("\n", stderr.Split('\n').TakeLast(5));
            Log.Error("download_stream: ffmpeg fehlgeschlagen (Code {0}). Letzte Ausgabe:\n{1}", proc.ExitCode, tail);
        }
    }
}
