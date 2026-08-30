using WebExStudio.Core.Models;
using WebExStudio.Engine.Actions;
using Xunit;
using EngineContext = WebExStudio.Engine.ExecutionContext;

namespace WebExStudio.Engine.Tests;

/// <summary>
/// Der Payload-Schnappschuss gehört nur an Einträge, die ihn brauchen.
///
/// WARUM: ContextSnapshot kopiert den ganzen Payload. Bei zwei Einträgen je Node hängt sonst bei
/// jeder Schleifenrunde ein weiteres Vielfaches des Payloads im Protokoll fest — Speicher, den
/// niemand liest, denn angezeigt wird er nur vom debug-Node. Bei Fehlern ist er dagegen die halbe
/// Diagnose und bleibt.
/// </summary>
public class TraceSnapshotTests
{
    private sealed class Handler(bool wirft) : IActionHandler
    {
        public string Type => "probe";
        public Task ExecuteAsync(EngineContext ctx, FlowNode node) =>
            wirft ? throw new InvalidOperationException("boom") : Task.CompletedTask;
    }

    private static async Task<TraceRecorder> Run(bool wirft)
    {
        var doc = new FlowDocument2
        {
            Tabs = [new FlowTab { Id = "main", Label = "Main", IsSubFlow = false }],
            Nodes = [new FlowNode { Id = "n", Type = "probe", TabId = "main" }],
        };
        var registry = ActionRegistry.CreateDefault().Register(new Handler(wirft));
        var rec = new TraceRecorder();
        var ctx = new EngineContext(page: null!, new TargetConfig { Name = "t" }, new RunConfig(), "",
            payload: new Dictionary<string, string> { ["gross"] = new string('x', 1000) },
            progress: rec)
        { Document = doc };
        await new FlowExecutor(registry).ExecuteWiredAsync(doc, "main", ctx);
        return rec;
    }

    [Fact]
    public async Task RoutineEintraege_TragenKeinePayloadKopie()
    {
        var rec = await Run(wirft: false);

        Assert.NotEmpty(rec.Entries);
        Assert.All(rec.Entries, e => Assert.Empty(e.ContextSnapshot));
    }

    [Fact]
    public async Task Fehlereintrag_BehaeltDenPayload()
    {
        var rec = await Run(wirft: true);

        var fehler = Assert.Single(rec.Entries, e => e.Status == ExecutionStatus.Error);
        Assert.Equal(1000, fehler.ContextSnapshot["gross"].Length);
    }
}
