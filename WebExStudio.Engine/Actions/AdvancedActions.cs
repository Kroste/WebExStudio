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

public sealed class ScreenshotHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "screenshot";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var path = ctx.Fmt(node.Get("path"));
        var selector = ctx.Fmt(node.Get("selector"));
        var fullPage = node.GetBool("full_page");

        // Pfad bestimmen: leer → Zeitstempel-Datei im Download-/Projektordner.
        if (string.IsNullOrEmpty(path))
        {
            var dir = string.IsNullOrEmpty(ctx.Config.DownloadDir)
                ? Path.Combine(ctx.ProjectDir, "screenshots")
                : ctx.Config.DownloadDir;
            path = Path.Combine(dir, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        }
        else if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(ctx.ProjectDir, path);
        }
        var dirName = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dirName)) Directory.CreateDirectory(dirName);

        if (!string.IsNullOrEmpty(selector))
        {
            Log.Info("screenshot (Element {0}): {1}", selector, path);
            await ctx.Page.Locator(selector).First.ScreenshotAsync(new() { Path = path, Timeout = ctx.Config.TimeoutMs });
        }
        else
        {
            Log.Info("screenshot (ganze Seite={0}): {1}", fullPage, path);
            await ctx.Page.ScreenshotAsync(new() { Path = path, FullPage = fullPage });
        }
        ctx.Set("screenshot_path", path);
    }
}

public sealed class EvalJsHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "eval_js";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var script = node.Get("script");
        if (string.IsNullOrWhiteSpace(script))
        {
            Log.Warn("eval_js: kein Script angegeben");
            return;
        }
        var ctxKey = node.Get("ctx_key");
        var selector = ctx.Fmt(node.Get("selector"));

        // Script wird NICHT per Fmt ersetzt — geschweifte Klammern sind in JS allgegenwärtig.
        Log.Debug("eval_js (selector='{0}')", selector);
        System.Text.Json.JsonElement? result = string.IsNullOrEmpty(selector)
            ? await ctx.Page.EvaluateAsync(script)
            : await ctx.Page.Locator(selector).First.EvaluateAsync(script);

        if (!string.IsNullOrEmpty(ctxKey))
        {
            var value = ToStringValue(result);
            ctx.Set(ctxKey, value);
            Log.Debug("eval_js → ctx[{0}] = '{1}'", ctxKey, value);
        }
    }

    /// <summary>Wandelt den JS-Rückgabewert in einen String fürs Payload (String roh, Rest als JSON).</summary>
    public static string ToStringValue(System.Text.Json.JsonElement? element)
    {
        if (element is not { } e) return string.Empty;
        return e.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => e.GetString() ?? string.Empty,
            System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined => string.Empty,
            _ => e.GetRawText(),
        };
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
