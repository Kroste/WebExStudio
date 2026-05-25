using Microsoft.Playwright;
using NLog;
using WebExStudio.Core.Models;

namespace WebExStudio.Engine.Actions;

public sealed class ClickHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "click";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var selector = ctx.Fmt(node.Get("selector"));
        if (string.IsNullOrEmpty(selector))
            selector = ctx.Fmt(node.Get("xpath"));
        var text = ctx.Fmt(node.Get("text"));
        var scroll = node.GetBool("scroll", true);

        ILocator locator;
        if (!string.IsNullOrEmpty(text))
        {
            Log.Debug("Klick auf Text: {0}", text);
            locator = ctx.Page.GetByText(text).First;
        }
        else
        {
            Log.Debug("Klick: {0}", selector);
            locator = ctx.Page.Locator(selector).First;
        }

        if (scroll)
            await locator.ScrollIntoViewIfNeededAsync(new() { Timeout = ctx.Config.TimeoutMs });

        // Download-Klick: auf das download-Event warten und speichern, BEVOR der Flow weiterläuft
        // (sonst schließt z. B. close_tab die Seite, bevor Playwright den Download erfasst).
        if (node.GetBool("expect_download") && ctx.SaveDownload is not null)
        {
            var downloadTimeout = int.TryParse(node.Get("download_timeout_ms", "60000"), out var dt) ? dt : 60000;
            // Erst auf den Download warten, DANN klicken (klassisches Playwright-Muster).
            var downloadTask = ctx.Page.WaitForDownloadAsync(new PageWaitForDownloadOptions { Timeout = downloadTimeout });
            try
            {
                // NoWaitAfter: nicht auf die (durch den Download abgebrochene) Navigation warten —
                // sonst läuft der Klick selbst in einen Timeout.
                await locator.ClickAsync(new() { Timeout = ctx.Config.TimeoutMs, NoWaitAfter = true });
            }
            catch (TimeoutException)
            {
                Log.Warn("Download-Klick lief in einen Timeout — versuche dennoch, den Download zu erfassen.");
            }
            try
            {
                var download = await downloadTask;
                await ctx.SaveDownload(download); // blockiert bis die Datei gespeichert ist
            }
            catch (TimeoutException)
            {
                Log.Warn("Kein Download-Event nach Klick (evtl. Service-Worker-Download wie MEGA).");
            }
            return;
        }

        await locator.ClickAsync(new() { Timeout = ctx.Config.TimeoutMs });
    }
}

public sealed class SendKeysHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "send_keys";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var selector = ctx.Fmt(node.Get("selector"));
        if (string.IsNullOrEmpty(selector))
        {
            var name = ctx.Fmt(node.Get("name"));
            if (!string.IsNullOrEmpty(name)) selector = $"[name=\"{name}\"]";
        }
        var value = ctx.Fmt(node.Get("value"));
        var clear = node.GetBool("clear", true);
        var append = node.GetBool("append", false);

        Log.Debug("SendKeys: {0} = '{1}'", selector, value);
        var locator = ctx.Page.Locator(selector).First;

        if (clear && !append)
            await locator.ClearAsync(new() { Timeout = ctx.Config.TimeoutMs });

        await locator.FillAsync(value, new() { Timeout = ctx.Config.TimeoutMs });
    }
}

public sealed class WaitForHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "wait_for";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var selector = ctx.Fmt(node.Get("selector"));
        var timeoutMs = int.TryParse(node.Get("timeout_ms"), out var t) ? t : ctx.Config.TimeoutMs;
        var stateStr = node.Get("state", "visible");

        var state = stateStr.ToLowerInvariant() switch
        {
            "hidden" => WaitForSelectorState.Hidden,
            "attached" => WaitForSelectorState.Attached,
            "detached" => WaitForSelectorState.Detached,
            _ => WaitForSelectorState.Visible,
        };

        Log.Debug("Warte auf: {0} [{1}] (timeout={2}ms)", selector, stateStr, timeoutMs);
        await ctx.Page.WaitForSelectorAsync(selector, new() { State = state, Timeout = timeoutMs });
    }
}

public sealed class SleepHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "sleep";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var secStr = ctx.Fmt(node.Get("seconds", "1"));
        if (double.TryParse(secStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var sec))
        {
            Log.Debug("Warte {0}s", sec);
            await Task.Delay(TimeSpan.FromSeconds(sec), ctx.CancellationToken);
        }
    }
}

public sealed class MenuPathHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "menu_path";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var pathStr = ctx.Fmt(node.Get("path"));
        var prefix = ctx.Fmt(node.Get("selector_prefix", ""));
        var openStrategy = node.Get("open_strategy", "hover").ToLowerInvariant();
        var match = node.Get("match", "exact").ToLowerInvariant();
        var clickLast = node.GetBool("click_last", true);
        var clickAll = openStrategy == "click_all";

        string[] parts;
        if (!string.IsNullOrEmpty(pathStr))
            parts = pathStr.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        else
            parts = node.GetStringList("items").Select(ctx.Fmt).ToArray();
        if (parts.Length == 0) return;

        Log.Debug("Menüpfad: {0} (strategy={1}, match={2})", string.Join(" > ", parts), openStrategy, match);
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var isLast = i == parts.Length - 1;

            var exact = match == "exact";
            var locator = string.IsNullOrEmpty(prefix)
                ? ctx.Page.GetByText(part, new() { Exact = exact }).First
                : ctx.Page.Locator($"{prefix} :text(\"{part}\")").First;

            // click_all → click every item; otherwise hover intermediate items.
            // The last item is clicked when click_last is set (or when clicking all).
            var shouldClick = clickAll || (isLast && clickLast);
            if (shouldClick)
                await locator.ClickAsync(new() { Timeout = ctx.Config.TimeoutMs });
            else
                await locator.HoverAsync(new() { Timeout = ctx.Config.TimeoutMs });

            await Task.Delay(200, ctx.CancellationToken);
        }
    }
}
