using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Plugin.Crypto.Nodes;

/// <summary>
/// Performs cryptographic operations: HMAC signatures, hashing, and encoding/decoding.
/// Designed for payment gateway integrations that require request signing.
/// </summary>
[NodeDefinition(
    Name = "Crypto",
    Description = "Compute HMAC signatures, hashes, and encode/decode data for payment gateway authentication",
    Icon = "fa-solid fa-lock")]
[NodeInput("input", DisplayName = "Input")]
[NodeOutput("output", DisplayName = "Output", Type = PortType.Object)]
[ConfigurationProperty("operation", "string",
    Description = "Cryptographic operation to perform",
    IsRequired = true,
    Options = "hmac-sha1,hmac-sha256,hmac-sha512,hmac-md5,sha1,sha256,sha512,md5,base64-encode,base64-decode,hex-encode,hex-decode,aes-gcm-decrypt,aes-cbc-decrypt")]
[ConfigurationProperty("key", "string",
    Description = "Secret key for HMAC/AES operations. Supports credential expressions.")]
[ConfigurationProperty("data", "string",
    Description = "Data to process. If omitted, uses input port data serialized to string. Supports expressions.")]
[ConfigurationProperty("encoding", "string",
    Description = "Output encoding for hash/HMAC results",
    Options = "hex,base64")]
[ConfigurationProperty("iv", "string",
    Description = "Initialization vector (IV) or nonce for AES operations. Hex or Base64 encoded.")]
[ConfigurationProperty("inputEncoding", "string",
    Description = "Encoding of the input data/ciphertext for AES operations",
    Options = "base64,hex")]
public class CryptoNode : INode
{
    private readonly string _id = Guid.NewGuid().ToString();

    public string Id => _id;
    public string Type => "crypto";
    public NodeCategory Category => NodeCategory.Action;

    public Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        var logger = CreateLogger(context);

        try
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            var operation = GetRequiredConfigValue<string>(input, "operation");
            var key = GetConfigValue<string>(input, "key");
            var data = GetConfigValue<string>(input, "data");
            var encoding = GetConfigValue<string>(input, "encoding") ?? "hex";
            var iv = GetConfigValue<string>(input, "iv");
            var inputEncoding = GetConfigValue<string>(input, "inputEncoding") ?? "base64";

            // If no explicit data, serialize the input
            if (string.IsNullOrEmpty(data))
            {
                data = input.Data.ValueKind is JsonValueKind.String
                    ? input.Data.GetString() ?? string.Empty
                    : input.Data.GetRawText();
            }

            logger.LogDebug("Crypto operation: {Operation}, encoding: {Encoding}, dataLength: {DataLength}",
                operation, encoding, data.Length);

            var result = operation.ToLowerInvariant() switch
            {
                "hmac-sha1" => ComputeHmac(HashAlgorithmName.SHA1, RequireKey(key, operation), data, encoding),
                "hmac-sha256" => ComputeHmac(HashAlgorithmName.SHA256, RequireKey(key, operation), data, encoding),
                "hmac-sha512" => ComputeHmac(HashAlgorithmName.SHA512, RequireKey(key, operation), data, encoding),
                "hmac-md5" => ComputeHmac(HashAlgorithmName.MD5, RequireKey(key, operation), data, encoding),
                "sha1" => ComputeHash(HashAlgorithmName.SHA1, data, encoding),
                "sha256" => ComputeHash(HashAlgorithmName.SHA256, data, encoding),
                "sha512" => ComputeHash(HashAlgorithmName.SHA512, data, encoding),
                "md5" => ComputeHash(HashAlgorithmName.MD5, data, encoding),
                "base64-encode" => Convert.ToBase64String(Encoding.UTF8.GetBytes(data)),
                "base64-decode" => DecodeBase64(data),
                "hex-encode" => Convert.ToHexStringLower(Encoding.UTF8.GetBytes(data)),
                "hex-decode" => DecodeHex(data),
                "aes-gcm-decrypt" => DecryptAesGcm(RequireKey(key, operation), data, RequireIv(iv, operation), inputEncoding),
                "aes-cbc-decrypt" => DecryptAesCbc(RequireKey(key, operation), data, RequireIv(iv, operation), inputEncoding),
                _ => throw new InvalidOperationException($"Unknown crypto operation: {operation}")
            };

            var output = new Dictionary<string, object>
            {
                ["result"] = result,
                ["algorithm"] = operation,
                ["encoding"] = encoding
            };

            return Task.FromResult(SuccessOutput(output));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(FailureOutput("Crypto operation was cancelled"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Crypto node error");
            return Task.FromResult(FailureOutput($"Crypto operation failed: {ex.Message}"));
        }
    }

    private static string ComputeHmac(HashAlgorithmName algorithm, string key, string data, string encoding)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);

        byte[] hash = algorithm.Name switch
        {
            "SHA1" => HMACSHA1.HashData(keyBytes, dataBytes),
            "SHA256" => HMACSHA256.HashData(keyBytes, dataBytes),
            "SHA512" => HMACSHA512.HashData(keyBytes, dataBytes),
            "MD5" => HMACMD5.HashData(keyBytes, dataBytes),
            _ => throw new ArgumentException($"Unsupported HMAC algorithm: {algorithm.Name}")
        };

        return EncodeBytes(hash, encoding);
    }

    private static string ComputeHash(HashAlgorithmName algorithm, string data, string encoding)
    {
        var dataBytes = Encoding.UTF8.GetBytes(data);

        byte[] hash = algorithm.Name switch
        {
            "SHA1" => SHA1.HashData(dataBytes),
            "SHA256" => SHA256.HashData(dataBytes),
            "SHA512" => SHA512.HashData(dataBytes),
            "MD5" => MD5.HashData(dataBytes),
            _ => throw new ArgumentException($"Unsupported hash algorithm: {algorithm.Name}")
        };

        return EncodeBytes(hash, encoding);
    }

    private static string EncodeBytes(byte[] bytes, string encoding) => encoding.ToLowerInvariant() switch
    {
        "base64" => Convert.ToBase64String(bytes),
        _ => Convert.ToHexStringLower(bytes) // "hex" is the default
    };

    private static string DecodeBase64(string data)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(data));
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"Invalid base64 input: {ex.Message}", ex);
        }
    }

    private static string DecodeHex(string data)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromHexString(data));
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"Invalid hex input: {ex.Message}", ex);
        }
    }

    private static string RequireKey(string? key, string operation)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException(
                $"The 'key' configuration property is required for operation '{operation}'");
        }

        return key;
    }

    private static string RequireIv(string? iv, string operation)
    {
        if (string.IsNullOrEmpty(iv))
        {
            throw new InvalidOperationException(
                $"The 'iv' configuration property is required for operation '{operation}'");
        }

        return iv;
    }

    private static byte[] DecodeInput(string data, string inputEncoding) => inputEncoding.ToLowerInvariant() switch
    {
        "hex" => Convert.FromHexString(data),
        _ => Convert.FromBase64String(data) // "base64" is the default
    };

    private static byte[] DecodeKey(string key)
    {
        // Try base64 first (most common for AES keys), fall back to hex, then UTF-8
        try
        {
            var bytes = Convert.FromBase64String(key);
            if (bytes.Length is 16 or 24 or 32)
                return bytes;
        }
        catch (FormatException) { }

        try
        {
            var bytes = Convert.FromHexString(key);
            if (bytes.Length is 16 or 24 or 32)
                return bytes;
        }
        catch (FormatException) { }

        // Fall back to UTF-8 encoding
        return Encoding.UTF8.GetBytes(key);
    }

    private static byte[] DecodeIv(string iv)
    {
        // Try base64 first, then hex
        try { return Convert.FromBase64String(iv); }
        catch (FormatException) { }

        try { return Convert.FromHexString(iv); }
        catch (FormatException) { }

        return Encoding.UTF8.GetBytes(iv);
    }

    /// <summary>
    /// Decrypts AES-GCM ciphertext. Expected format: ciphertext contains both the encrypted data
    /// and the 16-byte authentication tag appended at the end (standard GCM convention).
    /// </summary>
    private static string DecryptAesGcm(string key, string data, string iv, string inputEncoding)
    {
        var keyBytes = DecodeKey(key);
        var nonceBytes = DecodeIv(iv);
        var ciphertextWithTag = DecodeInput(data, inputEncoding);

        if (keyBytes.Length is not (16 or 24 or 32))
        {
            throw new InvalidOperationException(
                $"AES key must be 16, 24, or 32 bytes. Got {keyBytes.Length} bytes.");
        }

        if (nonceBytes.Length is not 12)
        {
            throw new InvalidOperationException(
                $"AES-GCM nonce must be 12 bytes. Got {nonceBytes.Length} bytes.");
        }

        // Standard convention: last 16 bytes are the auth tag
        const int tagSize = 16;
        if (ciphertextWithTag.Length < tagSize)
        {
            throw new InvalidOperationException("Ciphertext is too short to contain an authentication tag.");
        }

        var ciphertext = ciphertextWithTag.AsSpan(0, ciphertextWithTag.Length - tagSize);
        var tag = ciphertextWithTag.AsSpan(ciphertextWithTag.Length - tagSize);

        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(keyBytes, tagSize);
        aesGcm.Decrypt(nonceBytes, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    /// <summary>
    /// Decrypts AES-CBC ciphertext with PKCS7 padding.
    /// </summary>
    private static string DecryptAesCbc(string key, string data, string iv, string inputEncoding)
    {
        var keyBytes = DecodeKey(key);
        var ivBytes = DecodeIv(iv);
        var ciphertext = DecodeInput(data, inputEncoding);

        if (keyBytes.Length is not (16 or 24 or 32))
        {
            throw new InvalidOperationException(
                $"AES key must be 16, 24, or 32 bytes. Got {keyBytes.Length} bytes.");
        }

        if (ivBytes.Length != 16)
        {
            throw new InvalidOperationException(
                $"AES-CBC IV must be 16 bytes. Got {ivBytes.Length} bytes.");
        }

        using var aes = Aes.Create();
        aes.Key = keyBytes;
        aes.IV = ivBytes;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plaintext = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);

        return Encoding.UTF8.GetString(plaintext);
    }

    /// <summary>
    /// Creates a logger scoped to this node type from the execution context.
    /// </summary>
    private ILogger CreateLogger(IExecutionContext context) =>
        context.Services?.GetService(typeof(ILoggerFactory)) is ILoggerFactory factory
            ? factory.CreateLogger(GetType())
            : context.Logger;

    private static NodeOutput SuccessOutput(object data) => new()
    {
        Data = JsonSerializer.SerializeToElement(data),
        Success = true
    };

    private static NodeOutput FailureOutput(string errorMessage) => new()
    {
        Data = default,
        Success = false,
        ErrorMessage = errorMessage
    };

    private static T? GetConfigValue<T>(NodeInput input, string key)
    {
        if (input.Configuration.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return default;

        if (input.Configuration.TryGetProperty(key, out var value))
        {
            if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                return default;

            return JsonSerializer.Deserialize<T>(value.GetRawText());
        }

        return default;
    }

    private static T GetRequiredConfigValue<T>(NodeInput input, string key)
    {
        var value = GetConfigValue<T>(input, key);
        if (value is null)
            throw new InvalidOperationException($"Required configuration '{key}' is missing");
        return value;
    }
}
