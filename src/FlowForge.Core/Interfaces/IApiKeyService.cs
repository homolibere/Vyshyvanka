using FlowForge.Core.Models;

namespace FlowForge.Core.Interfaces;

/// <summary>
/// Service for API key management.
/// </summary>
public interface IApiKeyService
{
    Task<ApiKeyCreateResult> CreateAsync(Guid userId, string name, List<string>? scopes = null, DateTime? expiresAt = null, CancellationToken cancellationToken = default);
    Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<ApiKey>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApiKeyValidationResult> ValidateAsync(string apiKey, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of API key creation.
/// </summary>
public record ApiKeyCreateResult
{
    public bool Success { get; init; }
    public ApiKey? ApiKey { get; init; }
    public string? PlainTextKey { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of API key validation.
/// </summary>
public record ApiKeyValidationResult
{
    public bool IsValid { get; init; }
    public Guid? UserId { get; init; }
    public Guid? ApiKeyId { get; init; }
    public List<string> Scopes { get; init; } = [];
    public string? ErrorMessage { get; init; }
}
