using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WebExStudio.Core.Models;

namespace WebExStudio.Core.Credentials;

/// <summary>
/// Plattformneutraler, verschlüsselter Anmeldedaten-Tresor (AES-256-GCM, Schlüssel via PBKDF2 aus
/// einem Master-Passwort). Hält die Einträge nur im entsperrten Zustand im Speicher.
/// Datenmodell: Name → (Feld → Wert), z. B. "F95" → { "user": …, "password": …, "api": … }.
///
/// Der Tresor gehört zu <b>einem Flow</b>: Ver-/entschlüsselt wird der opake Blob in
/// <see cref="FlowDocument2.Credentials"/> (Base64). So liegen Flow und Passwörter zusammen, und die
/// Passwörter von Flow A landen nie in Flow B. Persistiert wird beim Speichern des Flows
/// (<see cref="Save"/> schreibt nur in das Dokument, nicht auf die Platte).
/// </summary>
public sealed class CredentialVault
{
    private FlowDocument2? _doc; // Hintergrundspeicher: _doc.Credentials (Base64-Blob)
    // null = verschlossen
    private Dictionary<string, Dictionary<string, string>>? _data;
    private string? _password;

    /// <summary>Bindet den Tresor an ein Flow-Dokument (verschließt vorher). Bei Flow-Wechsel aufrufen.</summary>
    public void Bind(FlowDocument2? doc)
    {
        Lock();
        _doc = doc;
    }

    /// <summary>Enthält der gebundene Flow bereits einen (verschlüsselten) Tresor?</summary>
    public bool HasData => !string.IsNullOrEmpty(_doc?.Credentials);

    public bool IsUnlocked => _data is not null;

    /// <summary>Entsperrt den Tresor des gebundenen Flows (oder beginnt leer, wenn noch keiner existiert).
    /// Wirft bei falschem Passwort eine <see cref="CryptographicException"/>.</summary>
    public void Unlock(string password)
    {
        if (string.IsNullOrEmpty(_doc?.Credentials))
        {
            _data = new(StringComparer.OrdinalIgnoreCase);
            _password = password;
            return;
        }
        _data = CredentialCrypto.Decrypt(Convert.FromBase64String(_doc.Credentials), password);
        _password = password;
    }

    public void Lock()
    {
        _data = null;
        _password = null;
    }

    /// <summary>Wert eines Feldes (z. B. "password") eines Eintrags, oder null.</summary>
    public string? Get(string name, string field) =>
        _data is not null
        && _data.TryGetValue(name, out var fields)
        && fields.TryGetValue(field, out var value)
            ? value : null;

    public IReadOnlyList<string> Names =>
        _data is null ? [] : _data.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Die Felder eines Eintrags (Kopie), oder null wenn nicht vorhanden/verschlossen.</summary>
    public IReadOnlyDictionary<string, string>? Entry(string name) =>
        _data is not null && _data.TryGetValue(name, out var f)
            ? new Dictionary<string, string>(f) : null;

    /// <summary>Legt einen Eintrag an/überschreibt ihn mit den gegebenen Feldern.</summary>
    public void SetEntry(string name, IReadOnlyDictionary<string, string> fields)
    {
        EnsureUnlocked();
        _data![name] = new Dictionary<string, string>(fields, StringComparer.OrdinalIgnoreCase);
    }

    public void RemoveEntry(string name)
    {
        EnsureUnlocked();
        _data!.Remove(name);
    }

    /// <summary>Schreibt den Tresor verschlüsselt in das gebundene Flow-Dokument
    /// (<see cref="FlowDocument2.Credentials"/>). Die Persistenz auf die Platte erfolgt beim
    /// Speichern des Flows. Leerer Tresor ⇒ Feld wird auf null gesetzt (kein Blob im Flow).</summary>
    public void Save()
    {
        EnsureUnlocked();
        if (_doc is null) throw new InvalidOperationException("Kein Flow an den Tresor gebunden.");
        _doc.Credentials = _data!.Count == 0
            ? null
            : Convert.ToBase64String(CredentialCrypto.Encrypt(_data!, _password!));
    }

    /// <summary>Ändert das Master-Passwort und speichert sofort neu verschlüsselt.</summary>
    public void ChangePassword(string newPassword)
    {
        EnsureUnlocked();
        _password = newPassword;
        Save();
    }

    private void EnsureUnlocked()
    {
        if (_data is null) throw new InvalidOperationException("Der Tresor ist verschlossen.");
    }
}

/// <summary>AES-256-GCM mit PBKDF2-Schlüsselableitung. Dateiformat: salt(16) | nonce(12) | tag(16) | ciphertext.</summary>
internal static class CredentialCrypto
{
    private const int SaltLen = 16, NonceLen = 12, TagLen = 16, KeyLen = 32, Iterations = 200_000;

    public static byte[] Encrypt(Dictionary<string, Dictionary<string, string>> data, string password)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(data);
        var salt = RandomNumberGenerator.GetBytes(SaltLen);
        var nonce = RandomNumberGenerator.GetBytes(NonceLen);
        var key = Derive(password, salt);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagLen];
        using (var aes = new AesGcm(key, TagLen))
            aes.Encrypt(nonce, plain, cipher, tag);

        var outBuf = new byte[SaltLen + NonceLen + TagLen + cipher.Length];
        Buffer.BlockCopy(salt, 0, outBuf, 0, SaltLen);
        Buffer.BlockCopy(nonce, 0, outBuf, SaltLen, NonceLen);
        Buffer.BlockCopy(tag, 0, outBuf, SaltLen + NonceLen, TagLen);
        Buffer.BlockCopy(cipher, 0, outBuf, SaltLen + NonceLen + TagLen, cipher.Length);
        return outBuf;
    }

    public static Dictionary<string, Dictionary<string, string>> Decrypt(byte[] blob, string password)
    {
        if (blob.Length < SaltLen + NonceLen + TagLen)
            throw new CryptographicException("Tresor-Datei ist beschädigt.");
        var salt = blob[..SaltLen];
        var nonce = blob[SaltLen..(SaltLen + NonceLen)];
        var tag = blob[(SaltLen + NonceLen)..(SaltLen + NonceLen + TagLen)];
        var cipher = blob[(SaltLen + NonceLen + TagLen)..];
        var key = Derive(password, salt);
        var plain = new byte[cipher.Length];
        using (var aes = new AesGcm(key, TagLen))
            aes.Decrypt(nonce, cipher, tag, plain); // wirft bei falschem Passwort/Manipulation

        return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(plain)
               is { } d
            ? new Dictionary<string, Dictionary<string, string>>(
                d.ToDictionary(kv => kv.Key,
                    kv => new Dictionary<string, string>(kv.Value, StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase)
            : new(StringComparer.OrdinalIgnoreCase);
    }

    private static byte[] Derive(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, KeyLen);
}
