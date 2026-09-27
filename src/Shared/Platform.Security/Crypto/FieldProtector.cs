using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Platform.Security.Crypto;

/// <summary>
/// Application-level field encryption (AES-256-GCM) for secrets and personal information held in
/// the database — MFA seeds, beneficiary identity numbers (SEC-007, SEC-011, POPIA). Also provides
/// a keyed hash (HMAC-SHA256) so protected identifiers can still be matched for duplicate checks
/// without decrypting them. The key comes from configuration/secret store, never source control.
/// </summary>
public interface IFieldProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedValue);
    string KeyedHash(string value);
}

public sealed class AesGcmFieldProtector : IFieldProtector
{
    private const string Prefix = "v1:";
    private readonly byte[] _encryptionKey;
    private readonly byte[] _hashKey;

    public AesGcmFieldProtector(string base64OrPassphraseKey)
    {
        if (string.IsNullOrWhiteSpace(base64OrPassphraseKey))
            throw new InvalidOperationException("A field-encryption key is required (Security:FieldEncryptionKey).");

        // Derive two independent 256-bit keys from the configured secret.
        var master = SHA256.HashData(Encoding.UTF8.GetBytes(base64OrPassphraseKey));
        _encryptionKey = HMACSHA256.HashData(master, Encoding.ASCII.GetBytes("platform/field-encryption"));
        _hashKey = HMACSHA256.HashData(master, Encoding.ASCII.GetBytes("platform/keyed-hash"));
    }

    public static AesGcmFieldProtector FromConfiguration(IConfiguration configuration, string key = "Security:FieldEncryptionKey") =>
        new(configuration[key] ?? string.Empty);

    public string Protect(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(_encryptionKey, 16))
        {
            aes.Encrypt(nonce, plainBytes, cipher, tag);
        }
        var combined = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, combined, nonce.Length + tag.Length, cipher.Length);
        return Prefix + Convert.ToBase64String(combined);
    }

    public string Unprotect(string protectedValue)
    {
        if (!protectedValue.StartsWith(Prefix, StringComparison.Ordinal))
            throw new CryptographicException("Unrecognised protected value format.");
        var combined = Convert.FromBase64String(protectedValue[Prefix.Length..]);
        var nonce = combined[..12];
        var tag = combined[12..28];
        var cipher = combined[28..];
        var plain = new byte[cipher.Length];
        using (var aes = new AesGcm(_encryptionKey, 16))
        {
            aes.Decrypt(nonce, cipher, tag, plain);
        }
        return Encoding.UTF8.GetString(plain);
    }

    public string KeyedHash(string value) =>
        Convert.ToHexString(HMACSHA256.HashData(_hashKey, Encoding.UTF8.GetBytes(value.Trim().ToUpperInvariant())));
}

public static class Masking
{
    /// <summary>Masks all but the last <paramref name="visible"/> characters (e.g. ID numbers in lists/reports).</summary>
    public static string Mask(string? value, int visible = 3)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= visible) return new string('*', value.Length);
        return new string('*', value.Length - visible) + value[^visible..];
    }
}
