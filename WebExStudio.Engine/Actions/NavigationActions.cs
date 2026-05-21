using Microsoft.Playwright;
using NLog;
using WebExStudio.Core.Models;

namespace WebExStudio.Engine.Actions;

public sealed class GotoHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "goto";

    public async Task ExecuteAsync(ExecutionContext ctx, ActionNode node)
    {
        var url = ctx.Fmt(node.GetString("url"));
        if (string.IsNullOrEmpty(url)) url = ctx.Get("host");

        Log.Info("Navigiere zu: {0}", url);
        await ctx.Page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = ctx.Config.TimeoutMs,
        });

        var waitMs = node.GetString("wait_ms");
        if (int.TryParse(waitMs, out var ms) && ms > 0)
            await Task.Delay(ms, ctx.CancellationToken);
    }
}

public sealed class OpenTabHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "open_tab";

    public async Task ExecuteAsync(ExecutionContext ctx, ActionNode node)
    {
        var url = ctx.Fmt(node.GetString("url"));
        Log.Debug("Neuer Tab: {0}", url);
        var newPage = await ctx.Page.Context.NewPageAsync();
        if (!string.IsNullOrEmpty(url))
            await newPage.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
    }
}

public sealed class CloseTabHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "close_tab";

    public async Task ExecuteAsync(ExecutionContext ctx, ActionNode node)
    {
        Log.Debug("Schließe Tab");
        await ctx.Page.CloseAsync();
    }
}

public sealed class GetLinksHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "get_links";

    public async Task ExecuteAsync(ExecutionContext ctx, ActionNode node)
    {
        var selector = ctx.Fmt(node.GetString("selector", "a"));
        var ctxKey = node.GetString("ctx_key", "link");
        var filter = ctx.Fmt(node.GetString("filter"));
        var maxStr = node.GetString("max", "500");
        var max = int.TryParse(maxStr, out var m) ? m : 500;

        var subActions = node.GetSubActions("actions");
        if (subActions.Count == 0) return;

        var elements = await ctx.Page.QuerySelectorAllAsync(selector);
        var links = new List<string>();

        foreach (var el in elements)
        {
            var href = await el.GetAttributeAsync("href");
            if (string.IsNullOrEmpty(href)) continue;
            if (!string.IsNullOrEmpty(filter) && !System.Text.RegularExpressions.Regex.IsMatch(href, filter)) continue;
            links.Add(href);
            if (links.Count >= max) break;
        }

        Log.Debug("get_links: {0} Links gefunden (selector='{1}', filter='{2}')", links.Count, selector, filter);
        foreach (var link in links)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            var child = ctx.CreateChild(new Dictionary<string, string> { [ctxKey] = link });
            await child.RunSubActions(subActions);
        }
    }
}
