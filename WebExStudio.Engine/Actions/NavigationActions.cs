using Microsoft.Playwright;
using NLog;
using WebExStudio.Core.Models;

namespace WebExStudio.Engine.Actions;

public sealed class GotoHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "goto";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var url = ctx.Fmt(node.Get("url"));
        if (string.IsNullOrEmpty(url)) url = ctx.Get("host");

        Log.Info("Navigiere zu: {0}", url);
        await ctx.Page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = ctx.Config.TimeoutMs,
        });

        var waitMs = node.Get("wait_ms");
        if (int.TryParse(waitMs, out var ms) && ms > 0)
            await Task.Delay(ms, ctx.CancellationToken);
    }
}

public sealed class OpenTabHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "open_tab";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var url = ctx.Fmt(node.Get("url"));
        Log.Debug("Neuer Tab: {0}", url);
        var newPage = await ctx.Page.Context.NewPageAsync();
        ctx.AttachDownloads?.Invoke(newPage); // Downloads aus diesem Tab im Zielordner speichern
        if (!string.IsNullOrEmpty(url))
            await newPage.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        // Switch the active page to the newly opened tab.
        ctx.Page = newPage;
    }
}

public sealed class CloseTabHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "close_tab";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        Log.Debug("Schließe Tab");
        var context = ctx.Page.Context;
        await ctx.Page.CloseAsync();
        // Switch back to a remaining page so subsequent actions have a valid page.
        var remaining = context.Pages.FirstOrDefault();
        if (remaining is not null)
            ctx.Page = remaining;
    }
}

public sealed class GetLinksHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "get_links";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var selector = ctx.Fmt(node.Get("selector", "a"));
        var ctxKey = node.Get("ctx_key", "link");
        var filter = ctx.Fmt(node.Get("filter"));
        var max = int.TryParse(node.Get("max", "500"), out var m) ? m : 500;

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

        Log.Debug("get_links: {0} Links (selector='{1}')", links.Count, selector);
        foreach (var link in links)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            var child = ctx.CreateChild(new Dictionary<string, string> { [ctxKey] = link });
            await ctx.FollowOutput(node, 0, child); // per-link output
        }
        await ctx.FollowOutput(node, 1); // done output
    }
}
