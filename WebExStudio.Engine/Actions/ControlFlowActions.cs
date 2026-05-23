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

        // Route to output port 0 (then) or 1 (else); downstream nodes are wired there.
        await ctx.FollowOutput(node, result ? 0 : 1);
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
            case "payload_equals":
            case "ctx_equals":
            {
                var payloadVal = ctx.Get(selector);
                return useRegex ? Regex.IsMatch(payloadVal, value) : string.Equals(payloadVal, value, StringComparison.OrdinalIgnoreCase);
            }
            case "payload_contains":
            case "ctx_contains":
            {
                var payloadVal = ctx.Get(selector);
                return useRegex ? Regex.IsMatch(payloadVal, value) : payloadVal.Contains(value, StringComparison.OrdinalIgnoreCase);
            }
            case "page_matches":
            {
                var text = await ctx.Page.TextContentAsync("body") ?? string.Empty;
                return Regex.IsMatch(text, value);
            }
            case "page_contains":
            {
                var text = await ctx.Page.TextContentAsync("body") ?? string.Empty;
                return text.Contains(value, StringComparison.OrdinalIgnoreCase);
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

        if (!int.TryParse(startStr, out var start)) start = 0;
        if (!int.TryParse(endStr, out var end)) end = 0;
        if (!int.TryParse(stepStr, out var step) || step == 0) step = 1;

        Log.Debug("for_range: {0}..{1} step={2} key={3}", start, end, step, ctxKey);
        for (var i = start; exclusive ? i < end : i <= end; i += step)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            var child = ctx.CreateChild(new Dictionary<string, string> { [ctxKey] = i.ToString() });
            await ctx.FollowOutput(node, 0, child); // body output, per iteration
        }
        await ctx.FollowOutput(node, 1); // done output
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

        var items = ParseItems(itemsRaw);
        Log.Debug("foreach: {0} Items, key={1}", items.Count, ctxKey);

        foreach (var (key, val, fields) in items)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            var extra = new Dictionary<string, string> { [ctxKey] = val };
            if (key != val) extra[$"{ctxKey}_key"] = key;
            // Spread an object item's fields into the payload so {payload.host} etc. resolve.
            if (fields != null)
                foreach (var kv in fields) extra[kv.Key] = kv.Value;
            var child = ctx.CreateChild(extra);
            await ctx.FollowOutput(node, 0, child); // body output, per item
        }
        await ctx.FollowOutput(node, 1); // done output
    }

    private static List<(string key, string value, Dictionary<string, string>? fields)> ParseItems(string raw)
    {
        raw = raw.Trim();
        if (raw.StartsWith('['))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<JsonElement>>(raw);
                if (list != null)
                    return list.Select((e, i) => (i.ToString(), e.ToString(), Spread(e))).ToList();
            }
            catch { /* fall through */ }
        }
        if (raw.StartsWith('{'))
        {
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(raw);
                if (dict != null)
                    return dict.Select(kv => (kv.Key, kv.Value, (Dictionary<string, string>?)null)).ToList();
            }
            catch { /* fall through */ }
        }
        return raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                  .Select((v, i) => (i.ToString(), v, (Dictionary<string, string>?)null))
                  .ToList();
    }

    /// <summary>If the element is a JSON object, returns its flattened key/value fields.</summary>
    private static Dictionary<string, string>? Spread(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object) return null;
        var d = new Dictionary<string, string>();
        foreach (var p in e.EnumerateObject())
        {
            d[p.Name] = p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString() ?? "",
                JsonValueKind.Number => p.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null or JsonValueKind.Undefined => "",
                _ => p.Value.GetRawText(),
            };
        }
        return d;
    }
}

public sealed class CallHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "call";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var target = node.Get("target");
        if (string.IsNullOrEmpty(target)) target = node.Get("targetTabId"); // legacy fallback
        var allowQuit = node.GetBool("allow_quit");

        if (string.IsNullOrEmpty(target))
        {
            Log.Warn("call: kein Ziel-Subnode angegeben");
            return;
        }

        // Resolve subnode by Name (or fall back to tab id).
        var tab = ctx.Document?.GetTabByName(target) ?? ctx.Document?.GetTab(target);
        if (tab is null)
        {
            Log.Warn("call: Subnode nicht gefunden: {0}", target);
            return;
        }

        if (ctx.CallStack.Contains(tab.Id))
            throw new InvalidOperationException($"Rekursion erkannt: Subnode {target}");

        Log.Info("call: Rufe Subnode auf: {0}", target);
        var child = ctx.CreateCallChild(tab.Id);

        try
        {
            await child.RunSubTab(tab.Id);
            Log.Info("call: Subnode abgeschlossen: {0}", target);
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
