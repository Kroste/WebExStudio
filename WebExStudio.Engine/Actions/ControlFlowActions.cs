using System.Text.Json;
using System.Text.RegularExpressions;
using WebExStudio.Core.Models;
using WebExStudio.Core.Serialization;

namespace WebExStudio.Engine.Actions;

public sealed class IfThenElseHandler : IActionHandler
{
    public string Type => "if_then_else";

    public async Task ExecuteAsync(ExecutionContext ctx, ActionNode node)
    {
        var condition = node.GetString("condition", "element_exists");
        var selector = ctx.Fmt(node.GetString("selector"));
        var value = ctx.Fmt(node.GetString("value"));
        var regex = node.GetBool("regex");
        var negate = node.GetBool("negate");

        var result = await EvaluateCondition(ctx, condition, selector, value, regex);
        if (negate) result = !result;

        var branch = result ? node.GetSubActions("then") : node.GetSubActions("else");
        if (branch.Count > 0)
            await ctx.RunSubActions(branch);
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
    public string Type => "for_range";

    public async Task ExecuteAsync(ExecutionContext ctx, ActionNode node)
    {
        var startStr = ctx.Fmt(node.GetString("start", "0"));
        var endStr = ctx.Fmt(node.GetString("end", "0"));
        var stepStr = ctx.Fmt(node.GetString("step", "1"));
        var ctxKey = node.GetString("ctx_key", "i");
        var exclusive = node.GetBool("exclusive");

        if (!int.TryParse(startStr, out var start)) start = 0;
        if (!int.TryParse(endStr, out var end)) end = 0;
        if (!int.TryParse(stepStr, out var step) || step == 0) step = 1;

        var subActions = node.GetSubActions("actions");
        if (subActions.Count == 0) return;

        for (var i = start; exclusive ? i < end : i <= end; i += step)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            var child = ctx.CreateChild(new Dictionary<string, string> { [ctxKey] = i.ToString() });
            await child.RunSubActions(subActions);
        }
    }
}

public sealed class ForeachHandler : IActionHandler
{
    public string Type => "foreach";

    public async Task ExecuteAsync(ExecutionContext ctx, ActionNode node)
    {
        var itemsRaw = ctx.Fmt(node.GetString("items"));
        var ctxKey = node.GetString("ctx_key", "item");
        var subActions = node.GetSubActions("actions");
        if (subActions.Count == 0) return;

        // Try to parse as JSON array or object; fallback to comma-separated
        var items = ParseItems(itemsRaw);

        foreach (var (key, val) in items)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            var extra = new Dictionary<string, string> { [ctxKey] = val };
            if (key != val) extra[$"{ctxKey}_key"] = key;
            var child = ctx.CreateChild(extra);
            await child.RunSubActions(subActions);
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
        // Comma-separated
        return raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                  .Select((v, i) => (i.ToString(), v))
                  .ToList();
    }
}

public sealed class CallHandler : IActionHandler
{
    public string Type => "call";

    public async Task ExecuteAsync(ExecutionContext ctx, ActionNode node)
    {
        var filePath = ctx.Fmt(node.GetString("file"));
        var allowQuit = node.GetBool("allow_quit");
        var fullPath = Path.IsPathRooted(filePath)
            ? filePath
            : Path.Combine(ctx.ProjectDir, filePath);

        if (ctx.CallStack.Contains(fullPath))
            throw new InvalidOperationException($"Rekursion erkannt: {fullPath}");

        var flow = await FlowSerializer.LoadAsync(fullPath);
        var child = ctx.CreateCallChild(fullPath);

        try
        {
            await child.RunSubActions(flow.Actions);
        }
        catch (QuitException) when (!allowQuit)
        {
            // suppress quit from called flow
        }
    }
}

public sealed class NoopHandler : IActionHandler
{
    public string Type => "noop";
    public Task ExecuteAsync(ExecutionContext ctx, ActionNode node) => Task.CompletedTask;
}

public sealed class QuitHandler : IActionHandler
{
    public string Type => "quit";
    public Task ExecuteAsync(ExecutionContext ctx, ActionNode node) => throw new QuitException();
}
