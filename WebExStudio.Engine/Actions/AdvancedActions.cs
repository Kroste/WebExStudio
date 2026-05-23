using Microsoft.Playwright;
using NLog;
using WebExStudio.Core.Models;

namespace WebExStudio.Engine.Actions;

public sealed class DownloadUrlHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "download_url";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var url = ctx.Fmt(node.Get("url"));
        var filename = ctx.Fmt(node.Get("filename"));
        var timeoutMs = int.TryParse(node.Get("timeout_ms", "60000"), out var t) ? t : 60000;

        if (string.IsNullOrEmpty(filename))
            filename = Path.GetFileName(new Uri(url).LocalPath);
        if (string.IsNullOrEmpty(filename))
            filename = "download";

        var downloadDir = string.IsNullOrEmpty(ctx.Config.DownloadDir)
            ? Path.Combine(ctx.ProjectDir, "downloads")
            : ctx.Config.DownloadDir;
        Directory.CreateDirectory(downloadDir);
        var destPath = Path.Combine(downloadDir, filename);

        Log.Info("download_url: {0} → {1}", url, destPath);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.CancellationToken);
        cts.CancelAfter(timeoutMs);

        using var http = new System.Net.Http.HttpClient();
        var bytes = await http.GetByteArrayAsync(url, cts.Token);
        await File.WriteAllBytesAsync(destPath, bytes, ctx.CancellationToken);
        Log.Debug("download_url: {0} bytes gespeichert: {1}", bytes.Length, destPath);
        ctx.Set("download_path", destPath);
    }
}

public sealed class CaptchaGuardHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "captcha_guard";

    private static readonly string[] CaptchaSelectors =
    [
        "iframe[src*='recaptcha']",
        "iframe[src*='hcaptcha']",
        "iframe[src*='turnstile']",
        ".g-recaptcha",
        ".h-captcha",
        "[data-sitekey]",
    ];

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var timeoutSec = int.TryParse(node.Get("timeout_s", "120"), out var t) ? t : 120;

        var detected = false;
        foreach (var sel in CaptchaSelectors)
        {
            var el = await ctx.Page.QuerySelectorAsync(sel);
            if (el is not null && await el.IsVisibleAsync())
            {
                detected = true;
                break;
            }
        }

        if (!detected)
        {
            Log.Debug("captcha_guard: kein CAPTCHA erkannt");
            return;
        }

        Log.Warn("captcha_guard: CAPTCHA erkannt, warte auf Lösung (timeout={0}s)", timeoutSec);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

        var deadline = DateTime.Now.AddSeconds(timeoutSec);
        while (DateTime.Now < deadline)
        {
            cts.Token.ThrowIfCancellationRequested();
            var stillPresent = false;
            foreach (var sel in CaptchaSelectors)
            {
                var el = await ctx.Page.QuerySelectorAsync(sel);
                if (el is not null && await el.IsVisibleAsync())
                {
                    stillPresent = true;
                    break;
                }
            }
            if (!stillPresent)
            {
                Log.Info("captcha_guard: CAPTCHA gelöst");
                return;
            }
            await Task.Delay(2000, cts.Token);
        }

        Log.Warn("captcha_guard: Timeout abgelaufen");
    }
}
