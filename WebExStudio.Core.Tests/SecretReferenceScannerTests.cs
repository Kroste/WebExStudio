using WebExStudio.Core.Credentials;
using WebExStudio.Core.Models;
using Xunit;

namespace WebExStudio.Core.Tests;

public class SecretReferenceScannerTests
{
    private static FlowDocument2 Doc(params Dictionary<string, string>[] configs) => new()
    {
        Tabs = [new FlowTab { Id = "main", Label = "Main", IsSubFlow = false }],
        Nodes = configs.Select((c, i) => new FlowNode { Id = $"n{i}", Type = "send_keys", TabId = "main", Config = c }).ToList(),
    };

    [Fact]
    public void FindsDistinctReferences_Sorted()
    {
        var doc = Doc(
            new Dictionary<string, string> { ["value"] = "{secret[F95].user}" },
            new Dictionary<string, string> { ["value"] = "{secret[F95].password}" },
            new Dictionary<string, string> { ["url"] = "https://x?t={secret[API].token}", ["other"] = "{secret[F95].user}" }); // Duplikat

        var refs = SecretReferenceScanner.Scan(doc);

        Assert.Equal(
            [new SecretRef("API", "token"), new SecretRef("F95", "password"), new SecretRef("F95", "user")],
            refs);
    }

    [Fact]
    public void Empty_WhenNoSecrets()
    {
        var refs = SecretReferenceScanner.Scan(Doc(new Dictionary<string, string> { ["value"] = "kein geheimnis {payload.x}" }));
        Assert.Empty(refs);
    }

    [Fact]
    public void TwoFieldsOfSameEntry_AreSeparate()
    {
        var refs = SecretReferenceScanner.Scan(Doc(new Dictionary<string, string> { ["a"] = "{secret[Mail].user} / {secret[Mail].password}" }));
        Assert.Equal([new SecretRef("Mail", "password"), new SecretRef("Mail", "user")], refs);
    }
}
