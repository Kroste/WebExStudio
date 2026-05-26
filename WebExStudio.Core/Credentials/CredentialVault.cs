using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WebExStudio.Core.Credentials;

/// <summary>
/// Plattformneutraler, verschlüsselter Anmeldedaten-Tresor (AES-256-GCM, Schlüssel via PBKDF2 aus
/// einem Master-Passwort). Hält die Einträge nur im entsperrten Zustand im Speicher.
/// Datenmodell: Name → (Feld → Wert), z. B. "F95" → { "user": …, "password": …, "api": … }.
/// </summary>
public sealed class CredentialVault
{
    private readonly string _path;
    // null = verschlossen
    private Dictionary<string, Dictionary<string, string>>? _data;
    private string? _password;

    public CredentialVault(string path) => _path = path;

    /// <summary>Existiert bereits eine Tresor-Datei?</summary>
    public bool FileExists => File.Exists(_path);

    public bool IsUnlocked => _data is not null;

    /// <summary>Entsperrt den Tresor (oder legt einen neuen leeren an, wenn keine Datei existiert).
    /// Wirft bei falschem Passwort eine <see cref="CryptographicException"/>.</summary>
    public void Unlock(string password)
    {
        if (!FileExists)
        {
            _data = new(StringComparer.OrdinalIgnoreCase);
            _password = password;
            return;
        }
        _data = CredentialCrypto.Decrypt(File.ReadAllBytes(_path), password);
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

    /// <summary>Schreibt den Tresor verschlüsselt auf die Platte.</summary>
    public void Save()
    {
        EnsureUnlocked();
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(_path, CredentialCrypto.Encrypt(_data!, _password!));
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
