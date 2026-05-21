using Microsoft.Playwright;
using WebExStudio.Core.Models;

namespace WebExStudio.Engine.Actions;

public sealed class ClickHandler : IActionHandler
{
    public string Type => "click";

    public async Task ExecuteAsync(ExecutionContext ctx, ActionNode node)
    {
        var selector = ctx.Fmt(node.GetString("selector"));
        if (string.IsNullOrEmpty(selector))
            selector = ctx.Fmt(node.GetString("xpath"));
        var text = ctx.Fmt(node.GetString("text"));
        var scroll = node.GetBool("scroll", true);

        ILocator locator;
        if (!string.IsNullOrEmpty(text))
            locator = ctx.Page.GetByText(text).First;
        else
            locator = ctx.Page.Locator(selector).First;

        if (scroll)
            await locator.ScrollIntoViewIfNeededAsync(new() { Timeout = ctx.Config.TimeoutMs });

        await locator.ClickAsync(new() { Timeout = ctx.Config.TimeoutMs });
    }
}

public sealed class SendKeysHandler : IActionHandler
{
    public string Type => "send_keys";

    public async Task ExecuteAsync(ExecutionContext ctx, ActionNode node)
    {
        var selector = ctx.Fmt(node.GetString("selector"));
        if (string.IsNullOrEmpty(selector))
        {
            var name = ctx.Fmt(node.GetString("name"));
            if (!string.IsNullOrEmpty(name)) selector = $"[name=\"{name}\"]";
        }
        var value = ctx.Fmt(node.GetString("value"));
        var clear = node.GetBool("clear", true);
        var append = node.GetBool("append", false);

        var locator = ctx.Page.Locator(selector).First;

        if (clear && !append)
            await locator.ClearAsync(new() { Timeout = ctx.Config.TimeoutMs });

        await locator.FillAsync(value, new() { Timeout = ctx.Config.TimeoutMs });
    }
}

public sealed class WaitForHandler : IActionHandler
{
    public string Type => "wait_for";

    public async Task ExecuteAsync(ExecutionContext ctx, ActionNode node)
    {
        var selector = ctx.Fmt(node.GetString("selector"));
        var timeoutMs = int.TryParse(node.GetString("timeout_ms"), out var t) ? t : ctx.Config.TimeoutMs;
        var stateStr = node.GetString("state", "visible");

        var state = stateStr.ToLowerInvariant() switch
        {
            "hidden" => WaitForSelectorState.Hidden,
            "attached" => WaitForSelectorState.Attached,
            "detached" => WaitForSelectorState.Detached,
            _ => WaitForSelectorState.Visible,
        };

        await ctx.Page.WaitForSelectorAsync(selector, new() { State = state, Timeout = timeoutMs });
    }
}

public sealed class SleepHandler : IActionHandler
{
    public string Type => "sleep";

    public async Task ExecuteAsync(ExecutionContext ctx, ActionNode node)
    {
        var secStr = ctx.Fmt(node.GetString("seconds", "1"));
        if (double.TryParse(secStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var sec))
            await Task.Delay(TimeSpan.FromSeconds(sec), ctx.CancellationToken);
    }
}

public sealed class MenuPathHandler : IActionHandler
{
    public string Type => "menu_path";

    public async Task ExecuteAsync(ExecutionContext ctx, ActionNode node)
    {
        var pathStr = ctx.Fmt(node.GetString("path"));
        var prefix = ctx.Fmt(node.GetString("selector_prefix", ""));
        string[] parts;
        if (!string.IsNullOrEmpty(pathStr))
            parts = pathStr.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        else
            parts = node.GetStringArray("items").Select(ctx.Fmt).ToArray();
        if (parts.Length == 0) return;

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var isLast = i == parts.Length - 1;
            var locator = string.IsNullOrEmpty(prefix)
                ? ctx.Page.GetByText(part).First
                : ctx.Page.Locator($"{prefix} :text(\"{part}\")").First;

            if (isLast)
                await locator.ClickAsync(new() { Timeout = ctx.Config.TimeoutMs });
            else
                await locator.HoverAsync(new() { Timeout = ctx.Config.TimeoutMs });

            await Task.Delay(200, ctx.CancellationToken);
        }
    }
}
