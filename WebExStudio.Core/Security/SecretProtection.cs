using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using NLog;

namespace WebExStudio.Core.Security;

/// <summary>
/// Schützt einzelne Geheimwerte (API-Key, Proxy-Passwort) für die persistente JSON-Ablage.
/// Windows: DPAPI (Per-User-Scope, keine externe Abhängigkeit). Linux/macOS: AES mit
/// deterministischem Schlüssel aus MachineName + UserName + projektspezifischer Salz-Konstante.
///
/// Das ist bewusst KEIN Ersatz für einen echten Keyring: ein Angreifer mit demselben
/// Benutzerkonto kommt an die Werte heran. Es verhindert aber, dass ein versehentlich
/// weitergegebener Config-Dump (Support-Anhang, Backup, Screenshot) die Schlüssel direkt
/// preisgibt — und genau das ist der Fall, der in der Praxis passiert.
///
/// Ablageformat: <c>v1:&lt;base64&gt;</c>. Werte ohne dieses Präfix gelten als Klartext aus
/// einer älteren Version und werden beim nächsten Speichern automatisch verschlüsselt
/// (siehe <see cref="UnprotectOrPlaintext"/>).
///
/// Der Anmeldedaten-Tresor (CredentialVault) bleibt davon unberührt — der hat ein eigenes,
/// stärkeres Verfahren mit benutzergewähltem Passwort.
/// </summary>
public static class SecretProtection
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private const string Prefix = "v1:";

    /// <summary>Projektspezifisch — verhindert, dass andere Kroste-Apps dieselben Werte entschlüsseln.</summary>
    private static readonly byte[] Salt = "webexstudio-secret-v1"u8.ToArray();

    /// <summary>Verschlüsselt einen Geheimwert. Leere Eingabe bleibt leer.</summary>
    public static string Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;
        try
        {
            var bytes = Encoding.UTF8.GetBytes(plaintext);
            var cipher = OperatingSystem.IsWindows() ? ProtectWindows(bytes) : ProtectAes(bytes);
            return Prefix + Convert.ToBase64String(cipher);
        }
        catch (Exception ex)
        {
            // Lieber gar nichts speichern als im Klartext: der Nutzer trägt den Wert neu ein.
            Log.Warn(ex, "Geheimwert konnte nicht verschlüsselt werden — wird NICHT gespeichert");
            return string.Empty;
        }
    }

    /// <summary>True, wenn der Wert noch unverschlüsselter Altbestand ist.</summary>
    public static bool IsPlaintext(string? stored)
        => !string.IsNullOrEmpty(stored) && !stored.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// Entschlüsselt einen mit <see cref="Protect"/> geschriebenen Wert. Werte ohne
    /// <c>v1:</c>-Präfix stammen aus einer Version vor der Verschlüsselung und werden
    /// unverändert durchgereicht (Migration beim nächsten Speichern).
    /// </summary>
    public static string UnprotectOrPlaintext(string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return string.Empty;
        if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
        {
            Log.Debug("Geheimwert lag im Klartext vor (Altbestand) — wird verschlüsselt nachgezogen");
            return stored;
        }

        try
        {
            var cipher = Convert.FromBase64String(stored[Prefix.Length..]);
            var plain = OperatingSystem.IsWindows() ? UnprotectWindows(cipher) : UnprotectAes(cipher);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex)
        {
            // Maschinen-/Benutzerwechsel oder korrupte Config: der Wert ist verloren, die App
            // läuft aber weiter — der Nutzer trägt ihn in den Einstellungen neu ein.
            Log.Warn(ex, "Geheimwert konnte nicht entschlüsselt werden (Maschinen-/Benutzerwechsel?) — wird leer behandelt");
            return string.Empty;
        }
    }

    // ---- Windows: DPAPI ----------------------------------------------------

    [SupportedOSPlatform("windows")]
    private static byte[] ProtectWindows(byte[] plain)
        => ProtectedData.Protect(plain, Salt, DataProtectionScope.CurrentUser);

    [SupportedOSPlatform("windows")]
    private static byte[] UnprotectWindows(byte[] cipher)
        => ProtectedData.Unprotect(cipher, Salt, DataProtectionScope.CurrentUser);

    // ---- Linux/macOS: AES-CBC mit Maschinen-/Benutzer-Bindung --------------

    private static byte[] ProtectAes(byte[] plain)
    {
        using var aes = Aes.Create();
        aes.Key = DeriveKey();
        aes.GenerateIV();
        using var enc = aes.CreateEncryptor();
        var body = enc.TransformFinalBlock(plain, 0, plain.Length);
        var result = new byte[aes.IV.Length + body.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(body, 0, result, aes.IV.Length, body.Length);
        return result;
    }

    private static byte[] UnprotectAes(byte[] cipher)
    {
        using var aes = Aes.Create();
        aes.Key = DeriveKey();
        var iv = new byte[16];
        if (cipher.Length <= iv.Length) throw new CryptographicException("Chiffrat zu kurz");
        Buffer.BlockCopy(cipher, 0, iv, 0, iv.Length);
        aes.IV = iv;
        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(cipher, iv.Length, cipher.Length - iv.Length);
    }

    private static byte[] DeriveKey()
        => SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{Environment.MachineName}|{Environment.UserName}|webexstudio"));
}
