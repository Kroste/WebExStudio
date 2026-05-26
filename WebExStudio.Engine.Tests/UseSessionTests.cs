using System.Text.Json;
using WebExStudio.Engine.Actions;
using Xunit;

namespace WebExStudio.Engine.Tests;

/// <summary>Sitzungs-Prüfung und Cookie-Parsing des use_session-Nodes (ohne Browser).</summary>
public class UseSessionTests
{
    [Fact]
    public void SessionFileUsable_False_WhenMissing()
    {
        Assert.False(UseSessionHandler.SessionFileUsable("/nicht/vorhanden.json", 0, out var reason));
        Assert.Equal("Datei fehlt", reason);
    }

    [Fact]
    public void SessionFileUsable_True_WhenFreshFileExists()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            Assert.True(UseSessionHandler.SessionFileUsable(tmp, 0, out _));
            Assert.True(UseSessionHandler.SessionFileUsable(tmp, 24, out _)); // jung genug
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public void SessionFileUsable_False_WhenTooOld()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            File.SetLastWriteTime(tmp, DateTime.Now.AddHours(-48));
            Assert.False(UseSessionHandler.SessionFileUsable(tmp, 24, out var reason));
            Assert.Contains("älter", reason);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public void ParseCookies_MapsFields_AndSkipsExpired()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var json = $$"""
        {
          "cookies": [
            { "name": "sid", "value": "abc", "domain": ".f95zone.to", "path": "/", "expires": {{now + 3600}}, "httpOnly": true, "secure": true, "sameSite": "Lax" },
            { "name": "alt", "value": "x", "domain": ".f95zone.to", "path": "/", "expires": {{now - 10}} },
            { "name": "session_only", "value": "y", "domain": ".f95zone.to", "path": "/", "expires": -1 }
          ],
          "origins": []
        }
        """;
        using var doc = JsonDocument.Parse(json);
        var cookies = UseSessionHandler.ParseCookies(doc.RootElement, now);

        // abgelaufenes 'alt' fehlt; 'sid' und 'session_only' bleiben
        Assert.Equal(2, cookies.Count);
        var sid = cookies.Single(c => c.Name == "sid");
        Assert.Equal("abc", sid.Value);
        Assert.Equal(".f95zone.to", sid.Domain);
        Assert.Equal(true, sid.HttpOnly);
    }

    [Fact]
    public void ParseCookies_Empty_WhenNoCookiesProperty()
    {
        using var doc = JsonDocument.Parse("""{ "origins": [] }""");
        Assert.Empty(UseSessionHandler.ParseCookies(doc.RootElement, 0));
    }
}
