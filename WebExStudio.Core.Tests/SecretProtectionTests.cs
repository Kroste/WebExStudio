using WebExStudio.Core.Security;
using Xunit;

namespace WebExStudio.Core.Tests;

public class SecretProtectionTests
{
    [Theory]
    [InlineData("sk-ant-api03-abcdef")]
    [InlineData("kurz")]
    [InlineData("mit Sonderzeichen: äöü ß / \\ \" '")]
    [InlineData("sehr langer Wert " + "0123456789")]
    public void Protect_Unprotect_LiefertOriginalZurueck(string plaintext)
    {
        var protectedValue = SecretProtection.Protect(plaintext);

        Assert.NotEqual(plaintext, protectedValue);
        Assert.StartsWith("v1:", protectedValue);
        Assert.Equal(plaintext, SecretProtection.UnprotectOrPlaintext(protectedValue));
    }

    [Fact]
    public void Protect_EnthaeltDenKlartextNichtMehr()
    {
        const string secret = "supergeheimes-passwort";
        var protectedValue = SecretProtection.Protect(secret);

        Assert.DoesNotContain(secret, protectedValue, StringComparison.Ordinal);
    }

    [Fact]
    public void Protect_ErzeugtProAufrufEinAnderesChiffrat()
    {
        // Zufälliger IV bzw. DPAPI-Entropie: gleiche Eingabe darf nicht dasselbe Chiffrat liefern,
        // sonst ließe sich aus zwei Config-Dateien ablesen, dass derselbe Schlüssel drinsteht.
        Assert.NotEqual(SecretProtection.Protect("gleich"), SecretProtection.Protect("gleich"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Protect_LeererWertBleibtLeer(string? input)
    {
        Assert.Equal(string.Empty, SecretProtection.Protect(input));
        Assert.Equal(string.Empty, SecretProtection.UnprotectOrPlaintext(input));
    }

    [Fact]
    public void UnprotectOrPlaintext_ReichtAltbestandOhnePraefixDurch()
    {
        // Migration: vor der Verschlüsselung geschriebene Werte müssen weiter nutzbar sein.
        Assert.Equal("alter-klartext-key", SecretProtection.UnprotectOrPlaintext("alter-klartext-key"));
    }

    [Fact]
    public void UnprotectOrPlaintext_KaputtesChiffratLiefertLeerStattAusnahme()
    {
        // Maschinen-/Benutzerwechsel: der Wert ist verloren, die App muss trotzdem starten.
        Assert.Equal(string.Empty, SecretProtection.UnprotectOrPlaintext("v1:das-ist-kein-base64!!"));
        Assert.Equal(string.Empty, SecretProtection.UnprotectOrPlaintext("v1:AAAA"));
    }
}
