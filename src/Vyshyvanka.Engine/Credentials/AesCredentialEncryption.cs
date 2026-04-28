using System.Security.Cryptography;
using System.Text;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Engine.Credentials;

/// <summary>
/// AES-256 encryption implementation for credential data.
/// Uses CBC mode with PKCS7 padding and random IV.
/// </summary>
public sealed class AesCredentialEncryption : ICredentialEncryption
{
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
        
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();
        
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        
        // Prepend IV to ciphertext for storage
        var result = new byte[aes.IV.Length + encryptedBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);
        
        return result;
    }
    
    /// <inheritdoc />
    public string Decrypt(byte[] cipherText)
    {
        ArgumentNullException.ThrowIfNull(cipherText);
        
        const int ivLength = 16; // AES block size
        
        if (cipherText.Length < ivLength + 1)
        {
            throw new ArgumentException(
                "Cipher text is too short to contain IV and encrypted data.",
                nameof(cipherText));
        }
        
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        
        // Extract IV from the beginning of ciphertext
        var iv = new byte[ivLength];
        Buffer.BlockCopy(cipherText, 0, iv, 0, ivLength);
        aes.IV = iv;
        
        // Extract encrypted data
        var encryptedLength = cipherText.Length - ivLength;
        var encryptedBytes = new byte[encryptedLength];
        Buffer.BlockCopy(cipherText, ivLength, encryptedBytes, 0, encryptedLength);
        
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
