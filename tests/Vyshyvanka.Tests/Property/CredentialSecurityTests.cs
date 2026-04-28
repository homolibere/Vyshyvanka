using System.Text;
using System.Text.Json;
using CsCheck;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Engine.Credentials;
using Vyshyvanka.Engine.Execution;

namespace Vyshyvanka.Tests.Property;

/// <summary>
/// Property-based tests for credential encryption and non-exposure.
/// Feature: vyshyvanka, Property 13: Credential Encryption and Non-Exposure
/// </summary>
public class CredentialSecurityTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Feature: vyshyvanka, Property 13: Credential Encryption and Non-Exposure
    /// For any stored credential, the credential data SHALL be encrypted using AES-256,
    /// and the encrypted bytes SHALL NOT contain the plaintext credential values.
    /// Validates: Requirements 6.1, 6.5
    /// </summary>
    [Fact]
    public void EncryptedData_DoesNotContainPlaintext()
    {
        GenCredentialData.Sample(credentialData =>
        {
            // Arrange
            var encryptionKey = AesCredentialEncryption.GenerateKey();
            var encryption = new AesCredentialEncryption(encryptionKey);
            var plaintext = JsonSerializer.Serialize(credentialData);
            
            // Act
            var encryptedBytes = encryption.Encrypt(plaintext);
            
            // Assert - Encrypted bytes should not contain plaintext
            var encryptedString = Encoding.UTF8.GetString(encryptedBytes);
            
            // Check that none of the credential values appear in the encrypted data
            foreach (var (key, value) in credentialData)
            {
                // Skip empty values
                if (string.IsNullOrEmpty(value)) continue;
                
                // The encrypted bytes should not contain the plaintext value
                Assert.DoesNotContain(value, encryptedString);
                
                // Also check the raw bytes don't contain the value bytes
                var valueBytes = Encoding.UTF8.GetBytes(value);
                Assert.False(ContainsSubsequence(encryptedBytes, valueBytes),
                    $"Encrypted data contains plaintext value: {value}");
            }
            
            // The encrypted data should not contain the full JSON
            Assert.DoesNotContain(plaintext, encryptedString);
            
        }, iter: 100);
    }

    /// <summary>
    /// Feature: vyshyvanka, Property 13: Credential Encryption and Non-Exposure
    /// For any credential data, encrypting then decrypting SHALL return the original value,
    /// proving AES-256 encryption is correctly implemented.
    /// Validates: Requirements 6.1
    /// </summary>
    [Fact]
    public void EncryptionRoundTrip_ReturnsOriginalValue()
    {
        GenCredentialData.Sample(credentialData =>
        {
            // Arrange
            var encryptionKey = AesCredentialEncryption.GenerateKey();
            var encryption = new AesCredentialEncryption(encryptionKey);
            var plaintext = JsonSerializer.Serialize(credentialData);
            
            // Act
            var encryptedBytes = encryption.Encrypt(plaintext);
            var decrypted = encryption.Decrypt(encryptedBytes);
            
            // Assert - Round-trip should return original value
            Assert.Equal(plaintext, decrypted);
            
            // Also verify the deserialized data matches
            var decryptedData = JsonSerializer.Deserialize<Dictionary<string, string>>(decrypted);
            Assert.NotNull(decryptedData);
            Assert.Equal(credentialData.Count, decryptedData.Count);
            foreach (var (key, value) in credentialData)
            {
                Assert.True(decryptedData.ContainsKey(key), $"Missing key: {key}");
                Assert.Equal(value, decryptedData[key]);
            }
            
        }, iter: 100);
    }

    /// <summary>
    /// Feature: vyshyvanka, Property 13: Credential Encryption and Non-Exposure
    /// For any Credential object, JSON serialization SHALL NOT expose the EncryptedData field.
    /// Validates: Requirements 6.5
    /// </summary>
    [Fact]
    public void CredentialSerialization_DoesNotExposeEncryptedData()
    {
        GenCredential.Sample(credential =>
        {
            // Act - Serialize the credential to JSON
            var json = JsonSerializer.Serialize(credential, SerializerOptions);
            
            // Assert - JSON should not contain "encryptedData" field
            Assert.DoesNotContain("encryptedData", json, StringComparison.OrdinalIgnoreCase);
            
            // Assert - JSON should not contain the raw encrypted bytes
            var base64Encrypted = Convert.ToBase64String(credential.EncryptedData);
            if (base64Encrypted.Length > 10) // Only check if there's meaningful data
            {
                Assert.DoesNotContain(base64Encrypted, json);
            }
            
            // Verify the JSON can be deserialized and doesn't have encrypted data
            var deserialized = JsonSerializer.Deserialize<JsonElement>(json);
            Assert.False(deserialized.TryGetProperty("encryptedData", out _),
                "Serialized JSON should not have encryptedData property");
            
        }, iter: 100);
    }


    /// <summary>
    /// Feature: vyshyvanka, Property 13: Credential Encryption and Non-Exposure
    /// For any two encryptions of the same plaintext, the encrypted bytes SHALL be different
    /// (due to random IV), preventing pattern analysis.
    /// Validates: Requirements 6.1
    /// </summary>
    [Fact]
    public void EncryptionWithRandomIV_ProducesDifferentCiphertext()
    {
        GenCredentialData.Sample(credentialData =>
        {
            // Arrange
            var encryptionKey = AesCredentialEncryption.GenerateKey();
            var encryption = new AesCredentialEncryption(encryptionKey);
            var plaintext = JsonSerializer.Serialize(credentialData);
            
            // Act - Encrypt the same plaintext twice
            var encrypted1 = encryption.Encrypt(plaintext);
            var encrypted2 = encryption.Encrypt(plaintext);
            
            // Assert - The two encryptions should produce different ciphertext
            // (due to random IV generation)
            Assert.False(encrypted1.SequenceEqual(encrypted2),
                "Two encryptions of the same plaintext should produce different ciphertext");
            
            // But both should decrypt to the same value
            var decrypted1 = encryption.Decrypt(encrypted1);
            var decrypted2 = encryption.Decrypt(encrypted2);
            Assert.Equal(plaintext, decrypted1);
            Assert.Equal(plaintext, decrypted2);
            
        }, iter: 100);
    }

    /// <summary>
    /// Feature: vyshyvanka, Property 13: Credential Encryption and Non-Exposure
    /// For any credential, the encrypted data length SHALL be greater than the plaintext length
    /// (due to IV prepending and padding), indicating encryption occurred.
    /// Validates: Requirements 6.1
    /// </summary>
    [Fact]
    public void EncryptedData_HasExpectedStructure()
    {
        GenCredentialData.Sample(credentialData =>
        {
            // Arrange
            var encryptionKey = AesCredentialEncryption.GenerateKey();
            var encryption = new AesCredentialEncryption(encryptionKey);
            var plaintext = JsonSerializer.Serialize(credentialData);
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            
            // Act
            var encryptedBytes = encryption.Encrypt(plaintext);
            
            // Assert - Encrypted data should be longer than plaintext
            // (16 bytes IV + padded ciphertext)
            Assert.True(encryptedBytes.Length >= plaintextBytes.Length + 16,
                $"Encrypted length ({encryptedBytes.Length}) should be >= plaintext length ({plaintextBytes.Length}) + 16 (IV)");
            
            // Assert - Encrypted data should be at least 17 bytes (16 IV + 1 byte minimum)
            Assert.True(encryptedBytes.Length >= 17,
                "Encrypted data should be at least 17 bytes (16 IV + minimum ciphertext)");
            
        }, iter: 100);
    }

    #region Helper Methods

    /// <summary>
    /// Checks if a byte array contains a subsequence.
    /// </summary>
    private static bool ContainsSubsequence(byte[] source, byte[] pattern)
    {
        if (pattern.Length == 0) return true;
        if (source.Length < pattern.Length) return false;
        
        for (int i = 0; i <= source.Length - pattern.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (source[i + j] != pattern[j])
                {
                    found = false;
                    break;
                }
            }
            if (found) return true;
        }
        return false;
    }

    #endregion

    /// <summary>
    /// Feature: vyshyvanka, Property 14: Credential Injection at Runtime
    /// For any node execution requiring credentials, the Workflow_Engine SHALL decrypt and provide
    /// the credential values to the node, and the credentials SHALL be available only during
    /// that node's execution scope.
    /// Validates: Requirements 6.4
    /// </summary>
    [Fact]
    public void CredentialInjection_ProvidesDecryptedValuesAtRuntime()
    {
        GenCredentialDataWithType.Sample(testData =>
        {
            // Arrange
            var (credentialType, credentialData) = testData;
            var credentialId = Guid.NewGuid();
            var encryptionKey = AesCredentialEncryption.GenerateKey();
            var encryption = new AesCredentialEncryption(encryptionKey);
            
            // Create the credential with encrypted data
            var plaintext = JsonSerializer.Serialize(credentialData);
            var encryptedData = encryption.Encrypt(plaintext);
            
            var credential = new Credential
            {
                Id = credentialId,
                Name = "TestCredential",
                Type = credentialType,
                EncryptedData = encryptedData,
                OwnerId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            // Create in-memory repository and service
            var repository = new InMemoryCredentialRepository();
            repository.AddCredential(credential);
            
            var credentialService = new CredentialService(repository, encryption);
            var credentialProvider = new CredentialProvider(credentialService);
            
            // Create execution context with the credential provider
            var executionContext = new Vyshyvanka.Engine.Execution.ExecutionContext(
                Guid.NewGuid(),
                Guid.NewGuid(),
                credentialProvider);
            
            // Act - Retrieve credentials through the provider (simulating node execution)
            var retrievedCredentials = credentialProvider
                .GetCredentialAsync(credentialId)
                .GetAwaiter()
                .GetResult();
            
            // Assert - Credentials should be available and match original values
            Assert.NotNull(retrievedCredentials);
            Assert.Equal(credentialData.Count, retrievedCredentials.Count);
            
            foreach (var (key, expectedValue) in credentialData)
            {
                Assert.True(retrievedCredentials.ContainsKey(key), 
                    $"Retrieved credentials should contain key '{key}'");
                Assert.Equal(expectedValue, retrievedCredentials[key]);
            }
            
            // Verify credentials are accessible via execution context
            var contextCredentials = executionContext.Credentials
                .GetCredentialAsync(credentialId)
                .GetAwaiter()
                .GetResult();
            
            Assert.NotNull(contextCredentials);
            Assert.Equal(credentialData.Count, contextCredentials.Count);
            
        }, iter: 100);
    }

    /// <summary>
    /// Feature: vyshyvanka, Property 14: Credential Injection at Runtime
    /// For any credential ID that does not exist, the credential provider SHALL return null,
    /// ensuring nodes handle missing credentials gracefully.
    /// Validates: Requirements 6.4
    /// </summary>
    [Fact]
    public void CredentialInjection_ReturnsNullForNonExistentCredential()
    {
        Gen.Guid.Sample(nonExistentId =>
        {
            // Arrange
            var encryptionKey = AesCredentialEncryption.GenerateKey();
            var encryption = new AesCredentialEncryption(encryptionKey);
            var repository = new InMemoryCredentialRepository();
            var credentialService = new CredentialService(repository, encryption);
            var credentialProvider = new CredentialProvider(credentialService);
            
            // Act - Try to retrieve non-existent credential
            var result = credentialProvider
                .GetCredentialAsync(nonExistentId)
                .GetAwaiter()
                .GetResult();
            
            // Assert - Should return null for non-existent credentials
            Assert.Null(result);
            
        }, iter: 100);
    }

    /// <summary>
    /// Feature: vyshyvanka, Property 14: Credential Injection at Runtime
    /// For any stored credential, retrieving it multiple times SHALL return the same
    /// decrypted values, ensuring consistency during node execution.
    /// Validates: Requirements 6.4
    /// </summary>
    [Fact]
    public void CredentialInjection_ConsistentValuesOnMultipleRetrieval()
    {
        GenCredentialDataWithType.Sample(testData =>
        {
            // Arrange
            var (credentialType, credentialData) = testData;
            var credentialId = Guid.NewGuid();
            var encryptionKey = AesCredentialEncryption.GenerateKey();
            var encryption = new AesCredentialEncryption(encryptionKey);
            
            var plaintext = JsonSerializer.Serialize(credentialData);
            var encryptedData = encryption.Encrypt(plaintext);
            
            var credential = new Credential
            {
                Id = credentialId,
                Name = "TestCredential",
                Type = credentialType,
                EncryptedData = encryptedData,
                OwnerId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            var repository = new InMemoryCredentialRepository();
            repository.AddCredential(credential);
            
            var credentialService = new CredentialService(repository, encryption);
            var credentialProvider = new CredentialProvider(credentialService);
            
            // Act - Retrieve credentials multiple times
            var retrieval1 = credentialProvider.GetCredentialAsync(credentialId).GetAwaiter().GetResult();
            var retrieval2 = credentialProvider.GetCredentialAsync(credentialId).GetAwaiter().GetResult();
            var retrieval3 = credentialProvider.GetCredentialAsync(credentialId).GetAwaiter().GetResult();
            
            // Assert - All retrievals should return the same values
            Assert.NotNull(retrieval1);
            Assert.NotNull(retrieval2);
            Assert.NotNull(retrieval3);
            
            foreach (var (key, value) in credentialData)
            {
                Assert.Equal(value, retrieval1[key]);
                Assert.Equal(value, retrieval2[key]);
                Assert.Equal(value, retrieval3[key]);
            }
            
        }, iter: 100);
    }

    #region Generators

    /// <summary>Generator for non-empty alphanumeric strings (for credential values).</summary>
    private static Gen<string> GenNonEmptyString(int minLength, int maxLength) =>
        Gen.Char['a', 'z'].Array[minLength, maxLength].Select(chars => new string(chars));

    /// <summary>Generator for credential-like strings (API keys, passwords, etc.).</summary>
    private static readonly Gen<string> GenCredentialValue =
        Gen.OneOf(
            // API key style
            GenNonEmptyString(20, 50),
            // Password style
            GenNonEmptyString(8, 30),
            // Token style (alphanumeric with some special chars)
            from prefix in GenNonEmptyString(5, 10)
            from suffix in GenNonEmptyString(10, 20)
            select $"{prefix}_{suffix}",
            // UUID style
            Gen.Guid.Select(g => g.ToString())
        );

    /// <summary>Generator for credential data dictionary.</summary>
    private static readonly Gen<Dictionary<string, string>> GenCredentialData =
        Gen.OneOf(
            // API Key credential
            from apiKey in GenCredentialValue
            select new Dictionary<string, string> { ["apiKey"] = apiKey },
            
            // OAuth2 credential
            from clientId in GenCredentialValue
            from clientSecret in GenCredentialValue
            from accessToken in GenCredentialValue
            select new Dictionary<string, string>
            {
                ["clientId"] = clientId,
                ["clientSecret"] = clientSecret,
                ["accessToken"] = accessToken
            },
            
            // Basic Auth credential
            from username in GenNonEmptyString(3, 20)
            from password in GenCredentialValue
            select new Dictionary<string, string>
            {
                ["username"] = username,
                ["password"] = password
            },
            
            // Custom headers credential
            from headerCount in Gen.Int[1, 5]
            from headers in Gen.Select(
                GenNonEmptyString(5, 15),
                GenCredentialValue,
                (k, v) => (Key: k, Value: v)).List[headerCount, headerCount]
            select headers.ToDictionary(h => h.Key, h => h.Value)
        );

    /// <summary>Generator for CredentialType.</summary>
    private static readonly Gen<CredentialType> GenCredentialType =
        Gen.OneOf(
            Gen.Const(CredentialType.ApiKey),
            Gen.Const(CredentialType.OAuth2),
            Gen.Const(CredentialType.BasicAuth),
            Gen.Const(CredentialType.CustomHeaders)
        );

    /// <summary>Generator for Credential with encrypted data.</summary>
    private static readonly Gen<Credential> GenCredential =
        from id in Gen.Guid
        from name in GenNonEmptyString(1, 50)
        from credType in GenCredentialType
        from credData in GenCredentialData
        from ownerId in Gen.Guid
        let encryptionKey = AesCredentialEncryption.GenerateKey()
        let encryption = new AesCredentialEncryption(encryptionKey)
        let plaintext = JsonSerializer.Serialize(credData)
        let encryptedData = encryption.Encrypt(plaintext)
        select new Credential
        {
            Id = id,
            Name = name,
            Type = credType,
            EncryptedData = encryptedData,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    /// <summary>Generator for credential data with matching type.</summary>
    private static readonly Gen<(CredentialType Type, Dictionary<string, string> Data)> GenCredentialDataWithType =
        Gen.OneOf(
            // API Key credential
            from apiKey in GenCredentialValue
            select (CredentialType.ApiKey, new Dictionary<string, string> { ["apiKey"] = apiKey }),
            
            // OAuth2 credential
            from clientId in GenCredentialValue
            from clientSecret in GenCredentialValue
            from accessToken in GenCredentialValue
            select (CredentialType.OAuth2, new Dictionary<string, string>
            {
                ["clientId"] = clientId,
                ["clientSecret"] = clientSecret,
                ["accessToken"] = accessToken
            }),
            
            // Basic Auth credential
            from username in GenNonEmptyString(3, 20)
            from password in GenCredentialValue
            select (CredentialType.BasicAuth, new Dictionary<string, string>
            {
                ["username"] = username,
                ["password"] = password
            }),
            
            // Custom headers credential
            from headerCount in Gen.Int[1, 5]
            from headers in Gen.Select(
                GenNonEmptyString(5, 15),
                GenCredentialValue,
                (k, v) => (Key: k, Value: v)).List[headerCount, headerCount]
            select (CredentialType.CustomHeaders, headers.ToDictionary(h => h.Key, h => h.Value))
        );

    #endregion

    #region Test Helpers

    /// <summary>
    /// In-memory credential repository for testing credential injection.
    /// </summary>
    private sealed class InMemoryCredentialRepository : ICredentialRepository
    {
        private readonly Dictionary<Guid, Credential> _credentials = [];

        public void AddCredential(Credential credential)
        {
            _credentials[credential.Id] = credential;
        }

        public Task<Credential> CreateAsync(Credential credential, CancellationToken cancellationToken = default)
        {
            _credentials[credential.Id] = credential;
            return Task.FromResult(credential);
        }

        public Task<Credential?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _credentials.TryGetValue(id, out var credential);
            return Task.FromResult(credential);
        }

        public Task<Credential> UpdateAsync(Credential credential, CancellationToken cancellationToken = default)
        {
            _credentials[credential.Id] = credential;
            return Task.FromResult(credential);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_credentials.Remove(id));
        }

        public Task<IReadOnlyList<Credential>> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default)
        {
            var result = _credentials.Values.Where(c => c.OwnerId == ownerId).ToList();
            return Task.FromResult<IReadOnlyList<Credential>>(result);
        }
    }

    #endregion
}
