using System.Security.Cryptography;
using WebExStudio.Core.Credentials;
using WebExStudio.Core.Models;
using WebExStudio.Core.Serialization;
using Xunit;

namespace WebExStudio.Core.Tests;

/// <summary>
/// Der Tresor ist an ein Flow-Dokument gebunden und verschlüsselt seine Daten in
/// <see cref="FlowDocument2.Credentials"/> (pro Flow, nicht global).
/// </summary>
public class CredentialVaultTests
{
    private static CredentialVault Bound(FlowDocument2 doc)
    {
        var v = new CredentialVault();
        v.Bind(doc);
        return v;
    }

    [Fact]
    public void SaveLockUnlock_RoundTripsViaDocument()
    {
        var doc = new FlowDocument2();
        var v = Bound(doc);
        v.Unlock("geheim");
        v.SetEntry("F95", new Dictionary<string, string> { ["user"] = "max", ["password"] = "p@ss" });
        v.SetEntry("Pixeldrain", new Dictionary<string, string> { ["api"] = "abc-123" });
        v.Save();
        v.Lock();

        Assert.False(v.IsUnlocked);
        Assert.Null(v.Get("F95", "user"));   // verschlossen → nichts lesbar
        Assert.NotNull(doc.Credentials);     // verschlüsselter Blob liegt im Dokument

        var v2 = Bound(doc);                  // anderer Tresor, gleicher Flow
        Assert.True(v2.HasData);
        v2.Unlock("geheim");
        Assert.Equal("max", v2.Get("F95", "user"));
        Assert.Equal("p@ss", v2.Get("F95", "password"));
        Assert.Equal("abc-123", v2.Get("Pixeldrain", "api"));
        Assert.Equal(["F95", "Pixeldrain"], v2.Names);
    }

    [Fact]
    public void Unlock_WrongPassword_Throws()
    {
        var doc = new FlowDocument2();
        var v = Bound(doc);
        v.Unlock("richtig");
        v.SetEntry("X", new Dictionary<string, string> { ["password"] = "s" });
        v.Save();

        Assert.ThrowsAny<CryptographicException>(() => Bound(doc).Unlock("falsch"));
    }

    [Fact]
    public void Get_IsCaseInsensitive_ForNameAndField()
    {
        var v = Bound(new FlowDocument2());
        v.Unlock("pw");
        v.SetEntry("F95", new Dictionary<string, string> { ["Password"] = "p" });
        Assert.Equal("p", v.Get("f95", "password"));
    }

    [Fact]
    public void NewFlow_NoData_StartsEmptyOnUnlock()
    {
        var doc = new FlowDocument2();
        var v = Bound(doc);
        Assert.False(v.HasData);
        v.Unlock("pw");
        Assert.True(v.IsUnlocked);
        Assert.Empty(v.Names);
    }

    [Fact]
    public void Bind_LocksPrevious_AndIsolatesPerFlow()
    {
        var a = new FlowDocument2();
        var b = new FlowDocument2();
        var v = Bound(a);
        v.Unlock("pw");
        v.SetEntry("A", new Dictionary<string, string> { ["password"] = "1" });
        v.Save();

        v.Bind(b);                       // Wechsel zu Flow B → verschließt
        Assert.False(v.IsUnlocked);
        v.Unlock("pw");
        Assert.Empty(v.Names);           // Flow B kennt die Secrets von Flow A nicht
        Assert.Null(b.Credentials);      // und hat keinen eigenen Blob
        Assert.NotNull(a.Credentials);   // Flow A unverändert
    }

    [Fact]
    public void EmptyVault_ClearsDocumentBlob()
    {
        var doc = new FlowDocument2();
        var v = Bound(doc);
        v.Unlock("pw");
        v.SetEntry("X", new Dictionary<string, string> { ["password"] = "s" });
        v.Save();
        Assert.NotNull(doc.Credentials);

        v.RemoveEntry("X");
        v.Save();
        Assert.Null(doc.Credentials);    // leerer Tresor ⇒ kein Blob im Flow
    }

    [Fact]
    public void Credentials_SurviveFlowSerialization()
    {
        var doc = new FlowDocument2();
        var v = Bound(doc);
        v.Unlock("pw");
        v.SetEntry("F95", new Dictionary<string, string> { ["password"] = "secret" });
        v.Save();

        // Flow speichern + laden (JSON-Round-Trip) → Tresor reist mit.
        var loaded = FlowSerializer2.Deserialize(FlowSerializer2.Serialize(doc));
        var v2 = Bound(loaded);
        Assert.True(v2.HasData);
        v2.Unlock("pw");
        Assert.Equal("secret", v2.Get("F95", "password"));
    }
}
