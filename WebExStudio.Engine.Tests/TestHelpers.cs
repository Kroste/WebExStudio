using WebExStudio.Core.Models;
using WebExStudio.Engine;
using EngineContext = WebExStudio.Engine.ExecutionContext;

namespace WebExStudio.Engine.Tests;

/// <summary>Synchronous IProgress that records every trace entry (deterministic for tests).</summary>
public sealed class TraceRecorder : IProgress<TraceEntry>
{
    public List<TraceEntry> Entries { get; } = [];
    public void Report(TraceEntry value) => Entries.Add(value);

    public int Running(string nodeId) =>
        Entries.Count(e => e.NodeId == nodeId && e.Status == ExecutionStatus.Running);
    public bool Ran(string nodeId) => Entries.Any(e => e.NodeId == nodeId);
}

public static class Ctx
{
    /// <summary>A browser-free execution context (Page = null) for pure-logic tests.</summary>
    public static EngineContext Make(Dictionary<string, string>? payload = null, IProgress<TraceEntry>? progress = null) =>
        new(page: null!, new TargetConfig { Name = "test" }, new RunConfig(), projectDir: "",
            payload: payload, progress: progress);
}
