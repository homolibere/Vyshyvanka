using FlowForge.Core.Models;

namespace FlowForge.Core.Interfaces;

/// <summary>
/// Repository for API key data access.
/// </summary>
public interface IApiKeyRepository
{
    Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default);
    Task<IEnumerable<ApiKey>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApiKey> CreateAsync(ApiKey apiKey, CancellationToken cancellationToken = default);
    Task<ApiKey> UpdateAsync(ApiKey apiKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
