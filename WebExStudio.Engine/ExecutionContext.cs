using System.Collections.Immutable;
using Microsoft.Playwright;
using NLog;
using WebExStudio.Core.Models;

namespace WebExStudio.Engine;

/// <summary>
/// Holds all runtime state for a single target execution.
/// Payload flows through wires between nodes; ctx is the shared mutable dict.
/// </summary>
public sealed class ExecutionContext
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly Dictionary<string, string> _ctx;
    private readonly IProgress<TraceEntry>? _progress;

    public IPage Page { get; }
    public TargetConfig Target { get; }
    public RunConfig Config { get; }
    public string ProjectDir { get; }
    public ImmutableHashSet<string> CallStack { get; }
    public CancellationToken CancellationToken { get; }

    /// <summary>Payload dict flowing through wires (Node-RED msg.payload style).</summary>
    public Dictionary<string, string> Payload { get; set; } = new();

    /// <summary>The document being executed — used for sub-tab lookup.</summary>
    public FlowDocument2? Document { get; init; }

    /// <summary>Callback to execute a sequential sub-flow tab.</summary>
    public Func<string, ExecutionContext, Task>? RunSubTabCallback { get; init; }

    public ExecutionContext(
        IPage page,
        TargetConfig target,
        RunConfig config,
        string projectDir,
        Dictionary<string, string>? ctx = null,
        ImmutableHashSet<string>? callStack = null,
        IProgress<TraceEntry>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Page = page;
        Target = target;
        Config = config;
        ProjectDir = projectDir;
        CallStack = callStack ?? ImmutableHashSet<string>.Empty;
        _progress = progress;
        CancellationToken = cancellationToken;

        _ctx = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in config.Ctx) _ctx[kv.Key] = kv.Value;
        foreach (var kv in target.Ctx) _ctx[kv.Key] = kv.Value;
        _ctx["name"] = target.Name;
        _ctx["host"] = target.Host;
        _ctx["actions_file"] = target.ActionsFile;
        if (ctx != null)
            foreach (var kv in ctx) _ctx[kv.Key] = kv.Value;
    }

    /// <summary>Substitutes {placeholder} and {payload.key} tokens in a string.</summary>
    public string Fmt(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        foreach (var kv in _ctx)
            value = value.Replace($"{{{kv.Key}}}", kv.Value, StringComparison.OrdinalIgnoreCase);
        foreach (var kv in Payload)
            value = value.Replace($"{{payload.{kv.Key}}}", kv.Value, StringComparison.OrdinalIgnoreCase);
        return value;
    }

    public string Get(string key, string fallback = "") =>
        _ctx.TryGetValue(key, out var v) ? v : fallback;

    public void Set(string key, string value) => _ctx[key] = value;

    public IReadOnlyDictionary<string, string> ContextSnapshot() =>
        new Dictionary<string, string>(_ctx);

    /// <summary>Executes all sequential nodes in the given sub-flow tab.</summary>
    public async Task RunSubTab(string tabId)
    {
        if (RunSubTabCallback is null || Document is null) return;
        Log.Debug("RunSubTab: {0}", tabId);
        await RunSubTabCallback(tabId, this);
    }

    /// <summary>Creates a child context for sub-action execution with extra variables.</summary>
    public ExecutionContext CreateChild(Dictionary<string, string>? extra = null)
    {
        var child = new ExecutionContext(Page, Target, Config, ProjectDir, MergeWith(extra),
            CallStack, _progress, CancellationToken)
        {
            Document = Document,
            RunSubTabCallback = RunSubTabCallback,
        };
        child.Payload = new Dictionary<string, string>(Payload);
        return child;
    }

    /// <summary>Creates a child context for a called tab, adding tabId to the callstack.</summary>
    public ExecutionContext CreateCallChild(string calleeTabId, Dictionary<string, string>? extra = null)
    {
        var child = new ExecutionContext(Page, Target, Config, ProjectDir, MergeWith(extra),
            CallStack.Add(calleeTabId), _progress, CancellationToken)
        {
            Document = Document,
            RunSubTabCallback = RunSubTabCallback,
        };
        child.Payload = new Dictionary<string, string>(Payload);
        return child;
    }

    public void Report(TraceEntry entry) => _progress?.Report(entry);

    private Dictionary<string, string> MergeWith(Dictionary<string, string>? extra)
    {
        var merged = new Dictionary<string, string>(_ctx, StringComparer.OrdinalIgnoreCase);
        if (extra != null)
            foreach (var kv in extra) merged[kv.Key] = kv.Value;
        return merged;
    }
}
