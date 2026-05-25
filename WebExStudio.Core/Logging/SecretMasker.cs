using System.Text.RegularExpressions;

namespace WebExStudio.Core.Logging;

/// <summary>
/// Maskiert Geheimnisse, bevor sie ins Log geschrieben werden: JSON-Felder mit
/// sensiblen Schlüsselnamen (Passwort, Token, API-Key …) sowie konkrete, übergebene
/// Geheimwerte (z. B. der aktuell konfigurierte API-Key / Proxy-Passwort).
/// </summary>
public static partial class SecretMasker
{
    public const string Mask_ = "***";

    // JSON-Felder, deren Schlüsselname auf ein Geheimnis hindeutet (Teil-Treffer, z. B.
    // "loginPassword", "userPwd", "apiKey"). Bewusst NICHT bloßes "key" (kollidiert mit
    // harmlosen Feldern wie ctx_key / debug.key).
    [GeneratedRegex(
        "(\"[^\"]*(?:passwor|pwd|secret|token|api[_-]?key|apikey|authorization)[^\"]*\"\\s*:\\s*)\"[^\"]*\"",
        RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveJsonField();

    /// <summary>
    /// Gibt <paramref name="text"/> mit maskierten Geheimnissen zurück. Zusätzliche konkrete
    /// Geheimwerte (z. B. API-Key) werden als ganze Zeichenkette ersetzt.
    /// </summary>
    public static string Mask(string? text, params string?[] literalSecrets)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;

        var result = SensitiveJsonField().Replace(text, $"$1\"{Mask_}\"");

        foreach (var secret in literalSecrets)
            if (!string.IsNullOrEmpty(secret) && secret.Length >= 4)
                result = result.Replace(secret, Mask_);

        return result;
    }
}
