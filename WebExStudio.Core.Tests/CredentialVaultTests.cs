using System.Security.Cryptography;
using WebExStudio.Core.Credentials;
using Xunit;

namespace WebExStudio.Core.Tests;

public class CredentialVaultTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"webex_vault_{Guid.NewGuid():N}.enc");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void SaveLockUnlock_RoundTrips()
    {
        var v = new CredentialVault(_path);
        v.Unlock("geheim");
        v.SetEntry("F95", new Dictionary<string, string> { ["user"] = "max", ["password"] = "p@ss" });
        v.SetEntry("Pixeldrain", new Dictionary<string, string> { ["api"] = "abc-123" });
        v.Save();
        v.Lock();

        Assert.False(v.IsUnlocked);
        Assert.Null(v.Get("F95", "user")); // verschlossen → nichts lesbar

        var v2 = new CredentialVault(_path);
        v2.Unlock("geheim");
        Assert.Equal("max", v2.Get("F95", "user"));
        Assert.Equal("p@ss", v2.Get("F95", "password"));
        Assert.Equal("abc-123", v2.Get("Pixeldrain", "api"));
        Assert.Equal(["F95", "Pixeldrain"], v2.Names);
    }

    [Fact]
    public void Unlock_WrongPassword_Throws()
    {
        var v = new CredentialVault(_path);
        v.Unlock("richtig");
        v.SetEntry("X", new Dictionary<string, string> { ["password"] = "s" });
        v.Save();

        var v2 = new CredentialVault(_path);
        Assert.ThrowsAny<CryptographicException>(() => v2.Unlock("falsch"));
    }

    [Fact]
    public void Get_IsCaseInsensitive_ForNameAndField()
    {
        var v = new CredentialVault(_path);
        v.Unlock("pw");
        v.SetEntry("F95", new Dictionary<string, string> { ["Password"] = "p" });
        Assert.Equal("p", v.Get("f95", "password"));
    }

    [Fact]
    public void NewVault_NoFile_StartsEmptyOnUnlock()
    {
        var v = new CredentialVault(_path);
        Assert.False(v.FileExists);
        v.Unlock("pw");
        Assert.True(v.IsUnlocked);
        Assert.Empty(v.Names);
    }
}
