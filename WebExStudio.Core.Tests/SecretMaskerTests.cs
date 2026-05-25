using WebExStudio.Core.Logging;
using Xunit;

namespace WebExStudio.Core.Tests;

public class SecretMaskerTests
{
    [Theory]
    [InlineData("""{ "password": "geheim" }""")]
    [InlineData("""{ "apiKey": "sk-123" }""")]
    [InlineData("""{ "api_key": "sk-123" }""")]
    [InlineData("""{ "token": "abc" }""")]
    [InlineData("""{ "Passwort": "x" }""")]
    public void Mask_RedactsSensitiveJsonFields(string json)
    {
        var masked = SecretMasker.Mask(json);
        Assert.Contains("\"***\"", masked);
        Assert.DoesNotContain("geheim", masked);
        Assert.DoesNotContain("sk-123", masked);
    }

    [Theory]
    [InlineData("""{ "loginPassword": "x" }""")]
    [InlineData("""{ "userPwd": "x" }""")]
    [InlineData("""{ "passwortFeld": "x" }""")]
    public void Mask_RedactsKeysContainingSecretWords(string json)
    {
        var masked = SecretMasker.Mask(json);
        Assert.Contains("\"***\"", masked);
        Assert.DoesNotContain("\"x\"", masked);
    }

    [Fact]
    public void Mask_KeepsHarmlessFields()
    {
        // ctx_key / debug.key sind keine Geheimnisse und bleiben erhalten.
        var json = """{ "key": "status", "ctx_key": "i", "value": "ok" }""";
        var masked = SecretMasker.Mask(json);
        Assert.Contains("status", masked);
        Assert.Contains("\"i\"", masked);
        Assert.Contains("ok", masked);
    }

    [Fact]
    public void Mask_RedactsLiteralSecrets()
    {
        var masked = SecretMasker.Mask("Anfrage mit Key sk-abcdef über Proxy", "sk-abcdef");
        Assert.DoesNotContain("sk-abcdef", masked);
        Assert.Contains("***", masked);
    }

    [Fact]
    public void Mask_IgnoresShortOrEmptyLiterals()
    {
        // Sehr kurze/leere "Geheimnisse" nicht ersetzen (sonst zerstückelt es harmlosen Text).
        var masked = SecretMasker.Mask("abc def", "", "ab");
        Assert.Equal("abc def", masked);
    }
}
