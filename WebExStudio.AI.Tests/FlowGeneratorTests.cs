using WebExStudio.AI;
using Xunit;

namespace WebExStudio.AI.Tests;

public class FlowGeneratorTests
{
    /// <summary>Returns a canned response regardless of the prompt.</summary>
    private sealed class FakeClient(string response) : ILlmClient
    {
        public string Name => "Fake";
        public Task<string> CompleteAsync(string s, string u, CancellationToken ct = default) =>
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
        var result = await gen.GenerateAsync("Seite öffnen und klicken");

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
        var result = await gen.GenerateAsync("egal");

        Assert.True(result.Success);
        Assert.Equal(2, result.Document!.Nodes.Count);
    }

    [Fact]
    public async Task Generate_ReportsValidationErrors_NotSuccess()
    {
        // Gültiges JSON, aber unbekannter Node-Typ → Dokument geparst, aber nicht gültig.
        var badType = ValidFlow.Replace("\"type\": \"click\"", "\"type\": \"teleport\"");
        var gen = new FlowGenerator(new FakeClient(badType));
        var result = await gen.GenerateAsync("egal");

        Assert.False(result.Success);
        Assert.NotNull(result.Document);
        Assert.Contains(result.Validation!.Errors, e => e.Code == "unknown-type");
    }

    [Fact]
    public async Task Generate_InvalidJson_Fails()
    {
        var gen = new FlowGenerator(new FakeClient("das ist überhaupt kein json"));
        var result = await gen.GenerateAsync("egal");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Null(result.Document);
    }

    [Fact]
    public async Task Generate_EmptyDescription_Fails()
    {
        var gen = new FlowGenerator(new FakeClient(ValidFlow));
        var result = await gen.GenerateAsync("   ");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }
}
