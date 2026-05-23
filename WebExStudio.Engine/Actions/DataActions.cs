using System.Text.RegularExpressions;
using NLog;
using WebExStudio.Core.Models;

namespace WebExStudio.Engine.Actions;

public sealed class GetValueHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "get_value";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var selector = ctx.Fmt(node.Get("selector"));
        var attr = node.Get("attr");
        var ctxKey = node.Get("ctx_key", "value");
        var regexPattern = ctx.Fmt(node.Get("regex"));
        var filter = ctx.Fmt(node.Get("filter"));

        var el = await ctx.Page.QuerySelectorAsync(selector);
        if (el is null)
        {
            Log.Warn("get_value: Element nicht gefunden: {0}", selector);
            return;
        }

        string raw = string.IsNullOrEmpty(attr)
            ? await el.TextContentAsync() ?? string.Empty
            : await el.GetAttributeAsync(attr) ?? string.Empty;

        raw = raw.Trim();

        if (!string.IsNullOrEmpty(filter))
            raw = ApplyFilter(raw, filter);

        if (!string.IsNullOrEmpty(regexPattern))
        {
            var match = Regex.Match(raw, regexPattern);
            if (match.Success)
                raw = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
        }

        Log.Debug("get_value: {0} → ctx[{1}] = '{2}'", selector, ctxKey, raw);
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
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "set_ctx";

    public Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var key = node.Get("key");
        var value = ctx.Fmt(node.Get("value"));
        if (!string.IsNullOrEmpty(key))
        {
            Log.Debug("set_ctx: {0} = '{1}'", key, value);
            ctx.Set(key, value);
        }
        return Task.CompletedTask;
    }
}

public sealed class SetPayloadHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "set_payload";

    public Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var key = node.Get("key");
        var value = ctx.Fmt(node.Get("value"));
        if (!string.IsNullOrEmpty(key))
        {
            Log.Debug("set_payload: {0} = '{1}'", key, value);
            ctx.Payload[key] = value;
        }
        return Task.CompletedTask;
    }
}

public sealed class DebugHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "debug";

    public Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var source = node.Get("source", "payload").ToLowerInvariant();
        var key = node.Get("key");
        var label = node.Get("label");

        string Render(IReadOnlyDictionary<string, string> d) =>
            !string.IsNullOrEmpty(key)
                ? $"{key}={(d.TryGetValue(key, out var v) ? v : "")}"
                : System.Text.Json.JsonSerializer.Serialize(d);

        var body = source switch
        {
            "ctx" => "ctx " + Render(ctx.ContextSnapshot()),
            "both" => "payload " + Render(ctx.Payload) + " | ctx " + Render(ctx.ContextSnapshot()),
            _ => "payload " + Render(ctx.Payload),
        };
        var msg = string.IsNullOrEmpty(label) ? body : $"{label}: {body}";

        Log.Info("debug: {0}", msg);
        ctx.Report(new TraceEntry(node.Id, "debug", ExecutionStatus.Success,
            DateTime.Now, ctx.Target.Name, ctx.ContextSnapshot(), Message: msg));
        return Task.CompletedTask;
    }
}

public sealed class ReadFileHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "read_file";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var path = ctx.Fmt(node.Get("path"));
        var ctxKey = node.Get("ctx_key", "file_content");
        var mode = node.Get("mode", "full");

        if (!Path.IsPathRooted(path))
            path = Path.Combine(ctx.ProjectDir, path);

        if (!File.Exists(path))
        {
            Log.Warn("read_file: Datei nicht gefunden: {0}", path);
            return;
        }

        Log.Debug("read_file: {0} mode={1} → ctx[{2}]", path, mode, ctxKey);
        var content = mode == "lines"
            ? string.Join('\n', await File.ReadAllLinesAsync(path, ctx.CancellationToken))
            : await File.ReadAllTextAsync(path, ctx.CancellationToken);

        ctx.Set(ctxKey, content);
    }
}

public sealed class WriteFileHandler : IActionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Type => "write_file";

    public async Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var path = ctx.Fmt(node.Get("path"));
        var value = ctx.Fmt(node.Get("value"));
        var append = node.GetBool("append");

        if (!Path.IsPathRooted(path))
            path = Path.Combine(ctx.ProjectDir, path);

        var dir = Path.GetDirectoryName(path);
        if (dir is not null) Directory.CreateDirectory(dir);

        Log.Debug("write_file: {0} (append={1})", path, append);
        if (append)
            await File.AppendAllTextAsync(path, value + Environment.NewLine, ctx.CancellationToken);
        else
            await File.WriteAllTextAsync(path, value, ctx.CancellationToken);
    }
}
