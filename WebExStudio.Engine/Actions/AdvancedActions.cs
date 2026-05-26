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

    /// <summary>Ein Timeout von 0 (oder negativ) bedeutet „kein Zeitlimit" — warte, bis gelöst.</summary>
    public static bool IsUnlimitedTimeout(int timeoutSec) => timeoutSec <= 0;

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var timeoutSec = int.TryParse(node.Get("timeout_s", "120"), out var t) ? t : 120;
        var unlimited = IsUnlimitedTimeout(timeoutSec);

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

        Log.Warn("captcha_guard: CAPTCHA erkannt, warte auf Lösung ({0})",
            unlimited ? "ohne Zeitlimit" : $"timeout={timeoutSec}s");

        // Erste Checkbox („Ich bin kein Roboter") selbst anklicken — reicht oft schon aus;
        // ein evtl. folgendes Bild-Rätsel löst der Nutzer dann im Wartefenster.
        if (node.GetBool("auto_click", true))
            await TryClickFirstCheckboxAsync(ctx);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.CancellationToken);
        if (!unlimited)
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

        // Ohne Zeitlimit läuft die Schleife bis zur Lösung oder bis der Nutzer „Stopp" drückt.
        var deadline = unlimited ? DateTime.MaxValue : DateTime.Now.AddSeconds(timeoutSec);
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

    // (iframe-Selektor, Checkbox-Selektor im iframe) für die gängigen CAPTCHA-Typen.
    private static readonly (string Frame, string Box)[] CheckboxTargets =
    [
        ("iframe[title='reCAPTCHA']", "#recaptcha-anchor"),
        ("iframe[src*='recaptcha'][src*='anchor']", "#recaptcha-anchor"),
        ("iframe[src*='hcaptcha']", "#checkbox"),
        ("iframe[src*='turnstile']", "input[type='checkbox']"),
        ("iframe[src*='challenges.cloudflare.com']", "input[type='checkbox']"),
    ];

    /// <summary>Klickt – falls vorhanden – die erste CAPTCHA-Checkbox im jeweiligen iframe.</summary>
    private static async Task<bool> TryClickFirstCheckboxAsync(ExecutionContext ctx)
    {
        foreach (var (frameSel, boxSel) in CheckboxTargets)
        {
            try
            {
                var box = ctx.Page.FrameLocator(frameSel).Locator(boxSel).First;
                await box.ClickAsync(new Microsoft.Playwright.LocatorClickOptions { Timeout = 3000 });
                Log.Info("captcha_guard: Checkbox geklickt ({0})", frameSel);
                await Task.Delay(1500, ctx.CancellationToken);
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                // Dieser Typ/Frame ist nicht vorhanden — nächsten versuchen.
            }
        }
        Log.Debug("captcha_guard: keine klickbare Checkbox gefunden");
        return false;
    }
}
