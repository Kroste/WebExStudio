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
        var text = ctx.FmtSecret(node.Get("text"));
        var scroll = node.GetBool("scroll", true);

        ILocator locator;
        if (!string.IsNullOrEmpty(text))
        {
            Log.Debug("Klick auf Text: {0}", ctx.MaskSecrets(text));
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
                // sonst läuft der Klick selbst in einen Timeout. (Wird laut Playwright künftig Standard;
                // bis dahin bewusst gesetzt → Obsolet-Warnung gezielt unterdrückt.)
#pragma warning disable CS0612
                await locator.ClickAsync(new() { Timeout = ctx.Config.TimeoutMs, NoWaitAfter = true });
#pragma warning restore CS0612
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
        var value = ctx.FmtSecret(node.Get("value")); // {secret[..]} hier auflösen (nie in den Payload)
        var clear = node.GetBool("clear", true);
        var append = node.GetBool("append", false);

        Log.Debug("SendKeys: {0} = '{1}'", selector, ctx.MaskSecrets(value));
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

public sealed class ScrollHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "scroll";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var selector = ctx.Fmt(node.Get("selector"));
        var to = node.Get("to", "bottom").ToLowerInvariant();
        var times = int.TryParse(node.Get("times", "1"), out var n) && n > 0 ? n : 1;
        var delayMs = int.TryParse(node.Get("delay_ms", "500"), out var d) ? d : 500;

        // Selektor hat Vorrang: gezielt zu einem Element scrollen.
        if (!string.IsNullOrEmpty(selector))
        {
            Log.Debug("Scroll zu Element: {0}", selector);
            await ctx.Page.Locator(selector).First.ScrollIntoViewIfNeededAsync(new() { Timeout = ctx.Config.TimeoutMs });
            return;
        }

        // Sonst Seite nach oben/unten scrollen — mehrfaches Scrollen nach unten lädt
        // „lazy" nachgeladene Inhalte (z. B. Forenlisten).
        var js = to == "top" ? "window.scrollTo(0, 0)" : "window.scrollTo(0, document.body.scrollHeight)";
        for (int i = 0; i < times; i++)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            Log.Debug("Scroll {0} ({1}/{2})", to, i + 1, times);
            await ctx.Page.EvaluateAsync(js);
            if (i < times - 1 && delayMs > 0)
                await Task.Delay(delayMs, ctx.CancellationToken);
        }
    }
}

public sealed class PressKeyHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "press_key";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var key = ctx.Fmt(node.Get("key"));
        if (string.IsNullOrEmpty(key))
        {
            Log.Warn("press_key: keine Taste angegeben");
            return;
        }
        var selector = ctx.Fmt(node.Get("selector"));

        if (!string.IsNullOrEmpty(selector))
        {
            Log.Debug("Taste '{0}' auf {1}", key, selector);
            await ctx.Page.Locator(selector).First.PressAsync(key, new() { Timeout = ctx.Config.TimeoutMs });
        }
        else
        {
            Log.Debug("Taste '{0}' (Seite)", key);
            await ctx.Page.Keyboard.PressAsync(key);
        }
    }
}

public sealed class SelectOptionHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "select_option";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var selector = ctx.Fmt(node.Get("selector"));
        var by = node.Get("by", "value").ToLowerInvariant();
        var value = ctx.FmtSecret(node.Get("value"));
        var locator = ctx.Page.Locator(selector).First;
        var options = new LocatorSelectOptionOptions { Timeout = ctx.Config.TimeoutMs };

        Log.Debug("select_option: {0} {1}='{2}'", selector, by, ctx.MaskSecrets(value));
        var choice = by switch
        {
            "label" => new SelectOptionValue { Label = value },
            "index" => new SelectOptionValue { Index = int.TryParse(value, out var i) ? i : 0 },
            _ => new SelectOptionValue { Value = value },
        };
        await locator.SelectOptionAsync(choice, options);
    }
}

public sealed class HoverHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "hover";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var text = ctx.Fmt(node.Get("text"));
        var selector = ctx.Fmt(node.Get("selector"));
        var locator = !string.IsNullOrEmpty(text)
            ? ctx.Page.GetByText(text).First
            : ctx.Page.Locator(selector).First;
        Log.Debug("Hover: {0}", string.IsNullOrEmpty(text) ? selector : $"text={text}");
        await locator.HoverAsync(new() { Timeout = ctx.Config.TimeoutMs });
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
