using System.Text.Json;
using System.Text.RegularExpressions;
using NLog;
using WebExStudio.Core.Models;

namespace WebExStudio.Engine.Actions;

public sealed class IfThenElseHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "if_then_else";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var condition = node.Get("condition", "element_exists");
        var selector = ctx.Fmt(node.Get("selector"));
        var value = ctx.Fmt(node.Get("value"));
        var regex = node.GetBool("regex");
        var negate = node.GetBool("negate");

        var result = await EvaluateCondition(ctx, condition, selector, value, regex);
        if (negate) result = !result;
        Log.Debug("if_then_else: {0} selector='{1}' → {2}", condition, selector, result);

        var tabId = result ? node.Get("thenTabId") : node.Get("elseTabId");
        if (!string.IsNullOrEmpty(tabId))
        {
            Log.Debug("if_then_else führt {0}-Tab aus: {1}", result ? "then" : "else", tabId);
            await ctx.RunSubTab(tabId);
        }
    }

    private static async Task<bool> EvaluateCondition(
        ExecutionContext ctx, string condition, string selector, string value, bool useRegex)
    {
        switch (condition.ToLowerInvariant())
        {
            case "element_exists":
            {
                var el = await ctx.Page.QuerySelectorAsync(selector);
                return el is not null;
            }
            case "element_visible":
            {
                var el = await ctx.Page.QuerySelectorAsync(selector);
                return el is not null && await el.IsVisibleAsync();
            }
            case "element_text":
            {
                var el = await ctx.Page.QuerySelectorAsync(selector);
                if (el is null) return false;
                var text = await el.TextContentAsync() ?? string.Empty;
                return useRegex ? Regex.IsMatch(text, value) : text.Contains(value, StringComparison.OrdinalIgnoreCase);
            }
            case "page_title":
            {
                var title = await ctx.Page.TitleAsync();
                return useRegex ? Regex.IsMatch(title, value) : title.Contains(value, StringComparison.OrdinalIgnoreCase);
            }
            case "page_url":
            {
                var url = ctx.Page.Url;
                return useRegex ? Regex.IsMatch(url, value) : url.Contains(value, StringComparison.OrdinalIgnoreCase);
            }
            case "ctx_equals":
            {
                var ctxVal = ctx.Get(selector);
                return useRegex ? Regex.IsMatch(ctxVal, value) : string.Equals(ctxVal, value, StringComparison.OrdinalIgnoreCase);
            }
            case "ctx_contains":
            {
                var ctxVal = ctx.Get(selector);
                return useRegex ? Regex.IsMatch(ctxVal, value) : ctxVal.Contains(value, StringComparison.OrdinalIgnoreCase);
            }
            default:
                return false;
        }
    }
}

public sealed class ForRangeHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "for_range";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var startStr = ctx.Fmt(node.Get("start", "0"));
        var endStr = ctx.Fmt(node.Get("end", "0"));
        var stepStr = ctx.Fmt(node.Get("step", "1"));
        var ctxKey = node.Get("ctx_key", "i");
        var exclusive = node.GetBool("exclusive");
        var bodyTabId = node.Get("bodyTabId");

        if (string.IsNullOrEmpty(bodyTabId)) return;

        if (!int.TryParse(startStr, out var start)) start = 0;
        if (!int.TryParse(endStr, out var end)) end = 0;
        if (!int.TryParse(stepStr, out var step) || step == 0) step = 1;

        Log.Debug("for_range: {0}..{1} step={2} key={3}", start, end, step, ctxKey);
        for (var i = start; exclusive ? i < end : i <= end; i += step)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            var child = ctx.CreateChild(new Dictionary<string, string> { [ctxKey] = i.ToString() });
            await child.RunSubTab(bodyTabId);
        }
    }
}

public sealed class ForeachHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "foreach";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var itemsRaw = ctx.Fmt(node.Get("items"));
        var ctxKey = node.Get("ctx_key", "item");
        var bodyTabId = node.Get("bodyTabId");

        if (string.IsNullOrEmpty(bodyTabId)) return;

        var items = ParseItems(itemsRaw);
        Log.Debug("foreach: {0} Items, key={1}", items.Count, ctxKey);

        foreach (var (key, val) in items)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            var extra = new Dictionary<string, string> { [ctxKey] = val };
            if (key != val) extra[$"{ctxKey}_key"] = key;
            var child = ctx.CreateChild(extra);
            await child.RunSubTab(bodyTabId);
        }
    }

    private static List<(string key, string value)> ParseItems(string raw)
    {
        raw = raw.Trim();
        if (raw.StartsWith('['))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<JsonElement>>(raw);
                if (list != null)
                    return list.Select((e, i) => (i.ToString(), e.ToString())).ToList();
            }
            catch { /* fall through */ }
        }
        if (raw.StartsWith('{'))
        {
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(raw);
                if (dict != null)
                    return dict.Select(kv => (kv.Key, kv.Value)).ToList();
            }
            catch { /* fall through */ }
        }
        return raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                  .Select((v, i) => (i.ToString(), v))
                  .ToList();
    }
}

public sealed class CallHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "call";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var targetTabId = node.Get("targetTabId");
        var allowQuit = node.GetBool("allow_quit");

        if (string.IsNullOrEmpty(targetTabId))
        {
            Log.Warn("call: kein targetTabId angegeben");
            return;
        }

        if (ctx.CallStack.Contains(targetTabId))
            throw new InvalidOperationException($"Rekursion erkannt: Tab {targetTabId}");

        Log.Info("call: Rufe Tab auf: {0}", targetTabId);
        var child = ctx.CreateCallChild(targetTabId);

        try
        {
            await child.RunSubTab(targetTabId);
            Log.Info("call: Tab abgeschlossen: {0}", targetTabId);
        }
        catch (QuitException) when (!allowQuit)
        {
            Log.Debug("call: QuitException unterdrückt (allow_quit=false)");
        }
    }
}

public sealed class NoopHandler : IActionHandler
{
    public string Type => "noop";
    public Task ExecuteAsync(ExecutionContext ctx, FlowNode node) => Task.CompletedTask;
}

public sealed class QuitHandler : IActionHandler
{
    public string Type => "quit";
    public Task ExecuteAsync(ExecutionContext ctx, FlowNode node) => throw new QuitException();
}
