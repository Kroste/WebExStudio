using WebExStudio.AI;
using Xunit;

namespace WebExStudio.AI.Tests;

public class FlowGeneratorTests
{
    /// <summary>Returns a canned response regardless of the conversation.</summary>
    private sealed class FakeClient(string response) : ILlmClient
    {
        public string Name => "Fake";
        public Task<string> ChatAsync(string s, IReadOnlyList<ChatMessage> m,
            bool jsonMode = false, CancellationToken ct = default) =>
            Task.FromResult(response);
    }

    private const string ValidFlow = """
        {
          "version": 2,
          "tabs": [{ "id": "main", "label": "Main", "isSubFlow": false }],
          "nodes": [
            { "id": "g", "type": "goto", "tabId": "main", "config": { "url": "https://example.com" }, "wires": [["c"]] },
            { "id": "c", "type": "click", "tabId": "main", "config": { "selector": "#go" }, "wires": [[]] }
          ]
        }
        """;

    [Fact]
    public async Task Generate_ParsesAndValidates_ValidFlow()
    {
        var gen = new FlowGenerator(new FakeClient(ValidFlow));
        var result = await gen.GenerateAsync("Seite öffnen und klicken", ct: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(result.Document);
        Assert.Equal(2, result.Document!.Nodes.Count);
        Assert.True(result.Validation!.IsValid);
    }

    [Fact]
    public async Task Generate_StripsMarkdownFences()
    {
        var fenced = "Hier ist dein Flow:\n```json\n" + ValidFlow + "\n```\nViel Erfolg!";
        var gen = new FlowGenerator(new FakeClient(fenced));
        var result = await gen.GenerateAsync("egal", ct: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.Document!.Nodes.Count);
    }

    [Fact]
    public async Task Generate_ReportsValidationErrors_NotSuccess()
    {
        // Gültiges JSON, aber unbekannter Node-Typ → Dokument geparst, aber nicht gültig.
        var badType = ValidFlow.Replace("\"type\": \"click\"", "\"type\": \"teleport\"");
        var gen = new FlowGenerator(new FakeClient(badType));
        var result = await gen.GenerateAsync("egal", ct: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.NotNull(result.Document);
        Assert.Contains(result.Validation!.Errors, e => e.Code == "unknown-type");
    }

    [Fact]
    public async Task Generate_InvalidJson_Fails()
    {
        var gen = new FlowGenerator(new FakeClient("das ist überhaupt kein json"));
        var result = await gen.GenerateAsync("egal", ct: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Null(result.Document);
    }

    [Fact]
    public async Task Generate_EmptyDescription_Fails()
    {
        var gen = new FlowGenerator(new FakeClient(ValidFlow));
        var result = await gen.GenerateAsync("   ", ct: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Parse_ExtractsFlowFromProse()
    {
        // Chat-Szenario: Fließtext mit eingebettetem Flow.
        var reply = "Klar! Hier ist der Flow:\n```json\n" + ValidFlow + "\n```\nFertig.";
        var result = FlowGenerator.Parse(reply);

        Assert.True(result.Success);
        Assert.Equal(2, result.Document!.Nodes.Count);
    }

    [Fact]
    public void Parse_FlowInFence_FollowedByBracesInProse()
    {
        // Regression: Erklärtext nach dem ```json-Block enthält {payload.link}-Klammern.
        // Ohne Fence-Bevorzugung würde „erstes { … letztes }" bis in den Erklärtext greifen.
        var reply = "Hier der geänderte Flow:\n```json\n" + ValidFlow + "\n```\n\n"
            + "**Was geändert wurde:** Der Node nutzt jetzt {payload.link_url} statt {payload.link}.";
        var result = FlowGenerator.Parse(reply);

        Assert.True(result.Success);
        Assert.Equal(2, result.Document!.Nodes.Count);
    }

    [Fact]
    public void Parse_PlainProse_IsNotAFlow()
    {
        var result = FlowGenerator.Parse("Ein foreach iteriert über eine Liste. Frag gern weiter!");
        Assert.False(result.Success);
        Assert.Null(result.Document);
    }

    [Fact]
    public async Task CompleteAsync_Extension_UsesJsonMode()
    {
        var spy = new ModeSpyClient();
        await spy.CompleteAsync("sys", "user", ct: TestContext.Current.CancellationToken);
        Assert.True(spy.LastJsonMode);
        Assert.Equal("user", spy.LastMessages.Single().Content);
    }

    private sealed class ModeSpyClient : ILlmClient
    {
        public string Name => "Spy";
        public bool LastJsonMode { get; private set; }
        public IReadOnlyList<ChatMessage> LastMessages { get; private set; } = [];

        public Task<string> ChatAsync(string s, IReadOnlyList<ChatMessage> m,
            bool jsonMode = false, CancellationToken ct = default)
        {
            LastJsonMode = jsonMode;
            LastMessages = m;
            return Task.FromResult("{}");
        }
    }
}
