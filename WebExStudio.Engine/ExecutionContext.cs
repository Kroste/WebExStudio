using System.Collections.Immutable;
using Microsoft.Playwright;
using NLog;
using WebExStudio.Core.Models;

namespace WebExStudio.Engine;

/// <summary>
/// Holds all runtime state for a single execution.
/// There is a single data store — the payload — that flows through wires and
/// is read/written by all nodes. Placeholders {key} and {payload.key} both
/// resolve against the payload.
/// </summary>
public sealed class ExecutionContext
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly IProgress<TraceEntry>? _progress;

    /// <summary>The active page. Handlers like open_tab/close_tab may switch it.</summary>
    public IPage Page { get; set; }
    public TargetConfig Target { get; }
    public RunConfig Config { get; }
    public string ProjectDir { get; }
    public ImmutableHashSet<string> CallStack { get; }
    public CancellationToken CancellationToken { get; }

    /// <summary>The single data store: payload flowing through the flow.</summary>
    public Dictionary<string, string> Payload { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The document being executed — used for sub-tab lookup.</summary>
    public FlowDocument2? Document { get; init; }

    /// <summary>Callback to execute a sequential sub-flow tab.</summary>
    public Func<string, ExecutionContext, Task>? RunSubTabCallback { get; init; }

    /// <summary>Callback to pause execution until the user resumes (used by the debug node).</summary>
    public Func<string, Task>? PauseCallback { get; init; }

    /// <summary>Pauses the flow (if a pause callback is wired) until the user resumes.</summary>
    public Task Pause(string message) => PauseCallback?.Invoke(message) ?? Task.CompletedTask;

    public ExecutionContext(
        IPage page,
        TargetConfig target,
        RunConfig config,
        string projectDir,
        Dictionary<string, string>? payload = null,
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

        if (payload != null)
            foreach (var kv in payload) Payload[kv.Key] = kv.Value;
    }

    /// <summary>Substitutes {key} and {payload.key} tokens with payload values.</summary>
    public string Fmt(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        foreach (var kv in Payload)
        {
            value = value.Replace($"{{payload.{kv.Key}}}", kv.Value, StringComparison.OrdinalIgnoreCase);
            value = value.Replace($"{{{kv.Key}}}", kv.Value, StringComparison.OrdinalIgnoreCase);
        }
        return value;
    }

    public string Get(string key, string fallback = "") =>
        Payload.TryGetValue(key, out var v) ? v : fallback;

    public void Set(string key, string value) => Payload[key] = value;

    public IReadOnlyDictionary<string, string> ContextSnapshot() =>
        new Dictionary<string, string>(Payload);

    /// <summary>Executes all sequential nodes in the given sub-flow tab.</summary>
    public async Task RunSubTab(string tabId)
    {
        if (RunSubTabCallback is null || Document is null) return;
        Log.Debug("RunSubTab: {0}", tabId);
        await RunSubTabCallback(tabId, this);
    }

    /// <summary>Creates a child context with extra payload values (e.g. loop variables).</summary>
    public ExecutionContext CreateChild(Dictionary<string, string>? extra = null) =>
        new(Page, Target, Config, ProjectDir, MergeWith(extra),
            CallStack, _progress, CancellationToken)
        {
            Document = Document,
            RunSubTabCallback = RunSubTabCallback,
            PauseCallback = PauseCallback,
        };

    /// <summary>Creates a child context for a called tab, adding tabId to the callstack.</summary>
    public ExecutionContext CreateCallChild(string calleeTabId, Dictionary<string, string>? extra = null) =>
        new(Page, Target, Config, ProjectDir, MergeWith(extra),
            CallStack.Add(calleeTabId), _progress, CancellationToken)
        {
            Document = Document,
            RunSubTabCallback = RunSubTabCallback,
            PauseCallback = PauseCallback,
        };

    public void Report(TraceEntry entry) => _progress?.Report(entry);

    private Dictionary<string, string> MergeWith(Dictionary<string, string>? extra)
    {
        var merged = new Dictionary<string, string>(Payload, StringComparer.OrdinalIgnoreCase);
        if (extra != null)
            foreach (var kv in extra) merged[kv.Key] = kv.Value;
        return merged;
    }
}
