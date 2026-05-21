using System.Text.RegularExpressions;
using WebExStudio.Core.Models;

namespace WebExStudio.Engine.Actions;

public sealed class GetValueHandler : IActionHandler
{
    public string Type => "get_value";

    public async Task ExecuteAsync(ExecutionContext ctx, ActionNode node)
    {
        var selector = ctx.Fmt(node.GetString("selector"));
        var attr = node.GetString("attr");
        var ctxKey = node.GetString("ctx_key", "value");
        var regexPattern = ctx.Fmt(node.GetString("regex"));
        var filter = ctx.Fmt(node.GetString("filter"));

        var el = await ctx.Page.QuerySelectorAsync(selector);
        if (el is null) return;

        string raw = string.IsNullOrEmpty(attr)
            ? await el.TextContentAsync() ?? string.Empty
            : await el.GetAttributeAsync(attr) ?? string.Empty;

        raw = raw.Trim();

        if (!string.IsNullOrEmpty(filter))
            raw = ApplyFilter(raw, filter);

        if (!string.IsNullOrEmpty(regexPattern))
        {
            var m = Regex.Match(raw, regexPattern);
            if (m.Success)
                raw = m.Groups.Count > 1 ? m.Groups[1].Value : m.Value;
        }

        ctx.Set(ctxKey, raw);
    }

    private static string ApplyFilter(string value, string filter) =>
        filter.ToLowerInvariant() switch
        {
            "trim" => value.Trim(),
            "lower" => value.ToLowerInvariant(),
            "upper" => value.ToUpperInvariant(),
            "digits" => new string(value.Where(char.IsDigit).ToArray()),
            _ => value,
        };
}

public sealed class SetCtxHandler : IActionHandler
{
    public string Type => "set_ctx";

    public Task ExecuteAsync(ExecutionContext ctx, ActionNode node)
    {
        var key = node.GetString("key");
        var value = ctx.Fmt(node.GetString("value"));
        if (!string.IsNullOrEmpty(key))
            ctx.Set(key, value);
        return Task.CompletedTask;
    }
}

public sealed class ReadFileHandler : IActionHandler
{
    public string Type => "read_file";

    public async Task ExecuteAsync(ExecutionContext ctx, ActionNode node)
    {
        var path = ctx.Fmt(node.GetString("path"));
        var ctxKey = node.GetString("ctx_key", "file_content");
        var mode = node.GetString("mode", "full");

        if (!Path.IsPathRooted(path))
            path = Path.Combine(ctx.ProjectDir, path);

        if (!File.Exists(path)) return;

        var content = mode == "lines"
            ? string.Join('\n', await File.ReadAllLinesAsync(path, ctx.CancellationToken))
            : await File.ReadAllTextAsync(path, ctx.CancellationToken);

        ctx.Set(ctxKey, content);
    }
}

public sealed class WriteFileHandler : IActionHandler
{
    public string Type => "write_file";

    public async Task ExecuteAsync(ExecutionContext ctx, ActionNode node)
    {
        var path = ctx.Fmt(node.GetString("path"));
        var value = ctx.Fmt(node.GetString("value"));
        var append = node.GetBool("append");

        if (!Path.IsPathRooted(path))
            path = Path.Combine(ctx.ProjectDir, path);

        var dir = Path.GetDirectoryName(path);
        if (dir is not null) Directory.CreateDirectory(dir);

        if (append)
            await File.AppendAllTextAsync(path, value + Environment.NewLine, ctx.CancellationToken);
        else
            await File.WriteAllTextAsync(path, value, ctx.CancellationToken);
    }
}
