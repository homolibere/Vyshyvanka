using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Core.Interfaces;

/// <summary>
/// Repository for API key data access.
/// </summary>
public interface IApiKeyRepository
{
    /// <summary>Gets an API key by unique identifier, or null when none exists.</summary>
    Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets an API key by its stored key hash, or null when none exists.</summary>
    Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default);

    /// <summary>Gets all API keys owned by the specified user.</summary>
    Task<IEnumerable<ApiKey>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Persists a new API key and returns the stored record.</summary>
    Task<ApiKey> CreateAsync(ApiKey apiKey, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing API key and returns the stored record.</summary>
    Task<ApiKey> UpdateAsync(ApiKey apiKey, CancellationToken cancellationToken = default);

    /// <summary>Deletes the API key with the given identifier.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
