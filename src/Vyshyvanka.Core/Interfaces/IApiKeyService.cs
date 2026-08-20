using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Core.Interfaces;

/// <summary>
/// Service for API key management.
/// </summary>
public interface IApiKeyService
{
    /// <summary>Creates a new API key for the user and returns the result, including the one-time plain-text key.</summary>
    Task<ApiKeyCreateResult> CreateAsync(Guid userId, string name, List<string>? scopes = null, DateTime? expiresAt = null, CancellationToken cancellationToken = default);

    /// <summary>Gets an API key by unique identifier, or null when none exists.</summary>
    Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets all API keys owned by the specified user.</summary>
    Task<IEnumerable<ApiKey>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Validates a presented plain-text API key and returns the associated identity when valid.</summary>
    Task<ApiKeyValidationResult> ValidateAsync(string apiKey, CancellationToken cancellationToken = default);

    /// <summary>Revokes the API key, deactivating it without deleting the record.</summary>
    Task RevokeAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Permanently deletes the API key.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of API key creation.
/// </summary>
public record ApiKeyCreateResult
{
    /// <summary>Whether the key was created successfully.</summary>
    public bool Success { get; init; }

    /// <summary>The created API key metadata when successful; otherwise null.</summary>
    public ApiKey? ApiKey { get; init; }

    /// <summary>The plain-text key value, returned only once at creation. Null on failure.</summary>
    public string? PlainTextKey { get; init; }

    /// <summary>Error message when creation failed. Null on success.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of API key validation.
/// </summary>
public record ApiKeyValidationResult
{
    /// <summary>Whether the presented key is valid and active.</summary>
    public bool IsValid { get; init; }

    /// <summary>Identifier of the user the key belongs to. Null when invalid.</summary>
    public Guid? UserId { get; init; }

    /// <summary>Identifier of the matched API key. Null when invalid.</summary>
    public Guid? ApiKeyId { get; init; }

    /// <summary>Scopes granted to the key.</summary>
    public List<string> Scopes { get; init; } = [];

    /// <summary>Error message when validation failed. Null when valid.</summary>
    public string? ErrorMessage { get; init; }
}
