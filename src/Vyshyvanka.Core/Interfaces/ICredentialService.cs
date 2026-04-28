using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Core.Interfaces;

/// <summary>
/// Service for managing credentials with encryption.
/// </summary>
public interface ICredentialService
{
    /// <summary>
    /// Creates a new credential with encrypted data.
    /// </summary>
    /// <param name="request">The credential creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created credential (without decrypted data).</returns>
    Task<Credential> CreateAsync(CreateCredentialRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a credential by ID (without decrypted data).
    /// </summary>
    /// <param name="id">The credential ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The credential or null if not found.</returns>
    Task<Credential?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a decrypted credential for use during execution.
    /// </summary>
    /// <param name="id">The credential ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The decrypted credential or null if not found.</returns>
    Task<DecryptedCredential?> GetDecryptedAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates an existing credential.
    /// </summary>
    /// <param name="id">The credential ID.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated credential.</returns>
    Task<Credential> UpdateAsync(Guid id, UpdateCredentialRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a credential and removes all encrypted data.
    /// </summary>
    /// <param name="id">The credential ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted, false if not found.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Lists all credentials for a user (without decrypted data).
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of credentials.</returns>
    Task<IReadOnlyList<Credential>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates credential data against the expected schema for the credential type.
    /// </summary>
    /// <param name="type">The credential type.</param>
    /// <param name="data">The credential data to validate.</param>
    /// <returns>Validation result with any errors.</returns>
    ValidationResult ValidateCredentialData(CredentialType type, Dictionary<string, string> data);
}
