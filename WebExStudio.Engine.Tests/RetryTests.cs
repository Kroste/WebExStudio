using WebExStudio.Core.Models;
using WebExStudio.Engine;
using WebExStudio.Engine.Actions;
using Xunit;
using EngineContext = WebExStudio.Engine.ExecutionContext;

namespace WebExStudio.Engine.Tests;

/// <summary>
/// Wiederholversuche pro Node (Config "retry"/"retry_delay_ms"). Ohne Browser: ein eigener
/// Handler („flaky") schlägt eine vorgegebene Anzahl Male fehl und zählt seine Aufrufe.
/// </summary>
public class RetryTests
{
    /// <summary>Handler, der die ersten <c>failTimes</c> Aufrufe wirft und danach erfolgreich ist.</summary>
    private sealed class FlakyHandler(int failTimes, Func<Exception>? error = null) : IActionHandler
    {
        public int Calls { get; private set; }
        public string Type => "flaky";

        public Task ExecuteAsync(EngineContext ctx, FlowNode node)
        {
            Calls++;
            if (Calls <= failTimes) throw (error ?? (() => new InvalidOperationException($"boom {Calls}")))();
            return Task.CompletedTask;
        }
    }

    private static FlowDocument2 Doc(Dictionary<string, string> cfg) => new()
    {
        Tabs = [new FlowTab { Id = "main", Label = "Main", IsSubFlow = false }],
        Nodes = [new FlowNode { Id = "n", Type = "flaky", TabId = "main", Config = cfg }],
    };

    private static async Task<TraceRecorder> Run(FlowDocument2 doc, FlakyHandler handler)
    {
        var registry = ActionRegistry.CreateDefault().Register(handler);
        var rec = new TraceRecorder();
        await new FlowExecutor(registry).RunTabAsync(doc, "main", page: null!, new RunConfig(),
            new TargetConfig { Name = "t" }, rec);
        return rec;
    }

    [Fact]
    public async Task SucceedsAfterTransientFailures()
    {
        var handler = new FlakyHandler(failTimes: 2);
        var rec = await Run(Doc(new() { ["retry"] = "3" }), handler);

        Assert.Equal(3, handler.Calls); // 2x Fehler, dann Erfolg
        Assert.Contains(rec.Entries, e => e.NodeId == "n" && e.Status == ExecutionStatus.Success);
        Assert.DoesNotContain(rec.Entries, e => e.NodeId == "n" && e.Status == ExecutionStatus.Error);
    }

    [Fact]
    public async Task ExhaustedRetries_ReportsError()
    {
        var handler = new FlakyHandler(failTimes: 5);
        var rec = await Run(Doc(new() { ["retry"] = "2" }), handler);

        Assert.Equal(3, handler.Calls); // 1 Versuch + 2 Wiederholungen
        Assert.Contains(rec.Entries, e => e.NodeId == "n" && e.Status == ExecutionStatus.Error);
    }

    [Fact]
    public async Task NoRetryByDefault_FailsImmediately()
    {
        var handler = new FlakyHandler(failTimes: 1);
        var rec = await Run(Doc([]), handler); // keine retry-Config

        Assert.Equal(1, handler.Calls);
        Assert.Contains(rec.Entries, e => e.NodeId == "n" && e.Status == ExecutionStatus.Error);
    }

    [Fact]
    public async Task QuitIsNotRetried()
    {
        var handler = new FlakyHandler(failTimes: 99, error: () => new QuitException());
        await Assert.ThrowsAsync<QuitException>(() => Run(Doc(new() { ["retry"] = "5" }), handler));

        Assert.Equal(1, handler.Calls); // Quit beendet sofort, keine Wiederholung
    }
}
