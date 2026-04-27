namespace FlowForge.Core.Interfaces;

/// <summary>
/// Provides encryption and decryption services for credential data.
/// </summary>
public interface ICredentialEncryption
{
    /// <summary>
    /// Encrypts plaintext credential data using AES-256.
    /// </summary>
    /// <param name="plainText">The plaintext data to encrypt.</param>
    /// <returns>Encrypted data including IV.</returns>
    byte[] Encrypt(string plainText);
    
    /// <summary>
    /// Decrypts encrypted credential data.
    /// </summary>
    /// <param name="cipherText">The encrypted data including IV.</param>
    /// <returns>Decrypted plaintext data.</returns>
    string Decrypt(byte[] cipherText);
}
