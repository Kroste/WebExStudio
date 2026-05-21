using System.Collections.Immutable;
using Microsoft.Playwright;
using NLog;
using WebExStudio.Core.Models;

namespace WebExStudio.Engine;

/// <summary>
/// Holds all runtime state for a single target execution.
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

    public Func<ExecutionContext, List<ActionNode>, Task>? RunSubActionsCallback { get; init; }

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
        // Merge: global < target < passed overrides
        foreach (var kv in config.Ctx) _ctx[kv.Key] = kv.Value;
        foreach (var kv in target.Ctx) _ctx[kv.Key] = kv.Value;
        _ctx["name"] = target.Name;
        _ctx["host"] = target.Host;
        _ctx["actions_file"] = target.ActionsFile;
        if (ctx != null)
            foreach (var kv in ctx) _ctx[kv.Key] = kv.Value;
    }

    /// <summary>Substitutes {placeholder} tokens in a string with context values.</summary>
    public string Fmt(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        foreach (var kv in _ctx)
            value = value.Replace($"{{{kv.Key}}}", kv.Value, StringComparison.OrdinalIgnoreCase);
        return value;
    }

    public string Get(string key, string fallback = "") =>
        _ctx.TryGetValue(key, out var v) ? v : fallback;

    public void Set(string key, string value) => _ctx[key] = value;

    public IReadOnlyDictionary<string, string> ContextSnapshot() =>
        new Dictionary<string, string>(_ctx);

    /// <summary>Creates a child context for sub-action execution with extra variables.</summary>
    public ExecutionContext CreateChild(Dictionary<string, string>? extra = null) =>
        new(Page, Target, Config, ProjectDir, MergeWith(extra), CallStack, _progress, CancellationToken)
        {
            RunSubActionsCallback = RunSubActionsCallback
        };

    /// <summary>Creates a child context for a called flow, adding the callee path to the callstack.</summary>
    public ExecutionContext CreateCallChild(string calleePath, Dictionary<string, string>? extra = null) =>
        new(Page, Target, Config, ProjectDir, MergeWith(extra), CallStack.Add(calleePath), _progress, CancellationToken)
        {
            RunSubActionsCallback = RunSubActionsCallback
        };

    public void Report(TraceEntry entry) => _progress?.Report(entry);

    public async Task RunSubActions(List<ActionNode> actions)
    {
        if (RunSubActionsCallback != null)
        {
            Log.Debug("RunSubActions: {0} Aktionen", actions.Count);
            await RunSubActionsCallback(this, actions);
        }
    }

    private Dictionary<string, string> MergeWith(Dictionary<string, string>? extra)
    {
        var merged = new Dictionary<string, string>(_ctx, StringComparer.OrdinalIgnoreCase);
        if (extra != null)
            foreach (var kv in extra) merged[kv.Key] = kv.Value;
        return merged;
    }
}
