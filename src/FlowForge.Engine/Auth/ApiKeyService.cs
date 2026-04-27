using System.Security.Cryptography;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;

namespace FlowForge.Engine.Auth;

/// <summary>
/// Service for API key generation and validation.
/// </summary>
public class ApiKeyService : IApiKeyService
{
    private readonly IApiKeyRepository _repository;
    private const string KeyPrefix = "ff_";

    public ApiKeyService(IApiKeyRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiKeyCreateResult> CreateAsync(
        Guid userId, 
        string name, 
        List<string>? scopes = null, 
        DateTime? expiresAt = null, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var plainTextKey = GenerateApiKey();
        var keyHash = HashApiKey(plainTextKey);

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            Name = name,
            KeyHash = keyHash,
            UserId = userId,
            Scopes = scopes ?? [],
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            IsActive = true
        };

        var created = await _repository.CreateAsync(apiKey, cancellationToken);

        return new ApiKeyCreateResult
        {
            Success = true,
            ApiKey = created,
            PlainTextKey = plainTextKey
        };
    }

    public async Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<IEnumerable<ApiKey>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByUserIdAsync(userId, cancellationToken);
    }

    public async Task<ApiKeyValidationResult> ValidateAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new ApiKeyValidationResult { IsValid = false, ErrorMessage = "API key is required" };
        }

        var keyHash = HashApiKey(apiKey);
        var storedKey = await _repository.GetByKeyHashAsync(keyHash, cancellationToken);

        if (storedKey is null)
        {
            return new ApiKeyValidationResult { IsValid = false, ErrorMessage = "Invalid API key" };
        }

        if (!storedKey.IsActive)
        {
            return new ApiKeyValidationResult { IsValid = false, ErrorMessage = "API key is revoked" };
        }

        if (storedKey.ExpiresAt.HasValue && storedKey.ExpiresAt.Value < DateTime.UtcNow)
        {
            return new ApiKeyValidationResult { IsValid = false, ErrorMessage = "API key has expired" };
        }

        // Update last used timestamp
        var updatedKey = storedKey with { LastUsedAt = DateTime.UtcNow };
        await _repository.UpdateAsync(updatedKey, cancellationToken);

        return new ApiKeyValidationResult
        {
            IsValid = true,
            UserId = storedKey.UserId,
            ApiKeyId = storedKey.Id,
            Scopes = storedKey.Scopes
        };
    }

    public async Task RevokeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var apiKey = await _repository.GetByIdAsync(id, cancellationToken);
        if (apiKey is not null)
        {
            var revokedKey = apiKey with { IsActive = false };
            await _repository.UpdateAsync(revokedKey, cancellationToken);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(id, cancellationToken);
    }

    private static string GenerateApiKey()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return KeyPrefix + Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static string HashApiKey(string apiKey)
    {
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(apiKey);
        var hashBytes = SHA256.HashData(keyBytes);
        return Convert.ToHexStringLower(hashBytes);
    }
}
