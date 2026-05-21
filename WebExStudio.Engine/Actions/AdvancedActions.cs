using System.Text.RegularExpressions;
using Microsoft.Playwright;
using WebExStudio.Core.Models;

namespace WebExStudio.Engine.Actions;

public sealed class DownloadUrlHandler : IActionHandler
{
    public string Type => "download_url";

    public async Task ExecuteAsync(ExecutionContext ctx, ActionNode node)
    {
        var url = ctx.Fmt(node.GetString("url"));
        var filename = ctx.Fmt(node.GetString("filename"));
        var timeoutMs = int.TryParse(node.GetString("timeout_ms", "60000"), out var t) ? t : 60000;

        if (string.IsNullOrEmpty(filename))
            filename = Path.GetFileName(new Uri(url).LocalPath);
        if (string.IsNullOrEmpty(filename))
            filename = "download";

        var downloadDir = string.IsNullOrEmpty(ctx.Config.DownloadDir)
            ? Path.Combine(ctx.ProjectDir, "downloads")
            : ctx.Config.DownloadDir;
        Directory.CreateDirectory(downloadDir);
        var destPath = Path.Combine(downloadDir, filename);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.CancellationToken);
        cts.CancelAfter(timeoutMs);

        using var http = new System.Net.Http.HttpClient();
        var bytes = await http.GetByteArrayAsync(url, cts.Token);
        await File.WriteAllBytesAsync(destPath, bytes, ctx.CancellationToken);
        ctx.Set("download_path", destPath);
    }
}

public sealed class CaptchaGuardHandler : IActionHandler
{
    public string Type => "captcha_guard";

    // Known CAPTCHA selectors
    private static readonly string[] CaptchaSelectors =
    [
        "iframe[src*='recaptcha']",
        "iframe[src*='hcaptcha']",
        "iframe[src*='turnstile']",
        ".g-recaptcha",
        ".h-captcha",
        "[data-sitekey]",
    ];

    public async Task ExecuteAsync(ExecutionContext ctx, ActionNode node)
    {
        var timeoutSec = int.TryParse(node.GetString("timeout_s", "120"), out var t) ? t : 120;

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

        if (!detected) return;

        // Wait for CAPTCHA to be resolved (user solves manually or auto-solver handles it)
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

        // Poll until CAPTCHA selectors disappear
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
            if (!stillPresent) return;
            await Task.Delay(2000, cts.Token);
        }
    }
}
