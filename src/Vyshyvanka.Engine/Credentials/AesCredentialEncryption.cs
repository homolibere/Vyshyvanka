using System.Security.Cryptography;
using System.Text;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Engine.Credentials;

/// <summary>
/// AES-256-GCM authenticated encryption implementation for credential data.
/// Provides confidentiality, integrity, and authenticity — immune to padding oracle attacks.
/// </summary>
/// <remarks>
/// Storage format (v2): [0x02] [nonce: 12 bytes] [tag: 16 bytes] [ciphertext]
/// Legacy format (v1):  [IV: 16 bytes] [ciphertext] — AES-CBC, decryption-only for migration.
/// </remarks>
public sealed class AesCredentialEncryption : ICredentialEncryption
{
    private const byte FormatVersion = 0x02;
    private const int NonceSize = 12; // AES-GCM standard nonce size
    private const int TagSize = 16;   // AES-GCM max tag size (128 bits)
    private const int LegacyIvSize = 16; // AES-CBC block size

    private readonly byte[] _key;

    /// <summary>
    /// Creates a new AES encryption service with the specified key.
    /// </summary>
    /// <param name="encryptionKey">Base64-encoded 256-bit (32-byte) encryption key.</param>
    /// <exception cref="ArgumentException">Thrown when the key is invalid.</exception>
    public AesCredentialEncryption(string encryptionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptionKey);

        try
        {
            _key = Convert.FromBase64String(encryptionKey);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Encryption key must be a valid Base64 string.", nameof(encryptionKey), ex);
        }

        if (_key.Length != 32)
        {
            throw new ArgumentException(
                $"Encryption key must be exactly 256 bits (32 bytes). Provided key is {_key.Length * 8} bits.",
                nameof(encryptionKey));
        }
    }

    /// <inheritdoc />
    public byte[] Encrypt(string plainText)
    {
        ArgumentNullException.ThrowIfNull(plainText);

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var cipherText = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Encrypt(nonce, plainBytes, cipherText, tag);

        // Format: [version (1)] [nonce (12)] [tag (16)] [ciphertext (N)]
        var result = new byte[1 + NonceSize + TagSize + cipherText.Length];
        result[0] = FormatVersion;
        Buffer.BlockCopy(nonce, 0, result, 1, NonceSize);
        Buffer.BlockCopy(tag, 0, result, 1 + NonceSize, TagSize);
        Buffer.BlockCopy(cipherText, 0, result, 1 + NonceSize + TagSize, cipherText.Length);

        return result;
    }

    /// <inheritdoc />
    public string Decrypt(byte[] cipherText)
    {
        ArgumentNullException.ThrowIfNull(cipherText);

        if (cipherText.Length < 2)
        {
            throw new ArgumentException(
                "Cipher text is too short to contain encrypted data.",
                nameof(cipherText));
        }

        return cipherText[0] == FormatVersion
            ? DecryptGcm(cipherText)
            : DecryptLegacyCbc(cipherText);
    }

    private string DecryptGcm(byte[] data)
    {
        const int headerSize = 1 + NonceSize + TagSize;

        if (data.Length < headerSize + 1)
        {
            throw new ArgumentException(
                "Cipher text is too short to contain nonce, tag, and encrypted data.");
        }

        var nonce = data.AsSpan(1, NonceSize);
        var tag = data.AsSpan(1 + NonceSize, TagSize);
        var cipherText = data.AsSpan(headerSize);

        var plainBytes = new byte[cipherText.Length];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Decrypt(nonce, cipherText, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// Decrypts data encrypted with the legacy AES-CBC format (v1).
    /// Retained only for backward compatibility during migration.
    /// </summary>
    private string DecryptLegacyCbc(byte[] data)
    {
        if (data.Length < LegacyIvSize + 1)
        {
            throw new ArgumentException(
                "Cipher text is too short to contain IV and encrypted data.");
        }

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var iv = new byte[LegacyIvSize];
        Buffer.BlockCopy(data, 0, iv, 0, LegacyIvSize);
        aes.IV = iv;

        var encryptedLength = data.Length - LegacyIvSize;
        var encryptedBytes = new byte[encryptedLength];
        Buffer.BlockCopy(data, LegacyIvSize, encryptedBytes, 0, encryptedLength);

        using var decryptor = aes.CreateDecryptor();
        var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

        return Encoding.UTF8.GetString(decryptedBytes);
    }

    /// <summary>
    /// Generates a new random 256-bit encryption key.
    /// </summary>
    /// <returns>Base64-encoded 256-bit key.</returns>
    public static string GenerateKey()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return Convert.ToBase64String(key);
    }
}
