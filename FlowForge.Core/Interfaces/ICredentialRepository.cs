using FlowForge.Core.Models;

namespace FlowForge.Core.Interfaces;

/// <summary>
/// Repository for persisting credentials.
/// </summary>
public interface ICredentialRepository
{
    /// <summary>Creates a new credential.</summary>
    Task<Credential> CreateAsync(Credential credential, CancellationToken cancellationToken = default);
    
    /// <summary>Gets a credential by ID.</summary>
    Task<Credential?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>Updates an existing credential.</summary>
    Task<Credential> UpdateAsync(Credential credential, CancellationToken cancellationToken = default);
    
    /// <summary>Deletes a credential by ID.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>Gets all credentials for a user.</summary>
    Task<IReadOnlyList<Credential>> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default);
}
