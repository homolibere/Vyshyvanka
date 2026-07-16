using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Engine.Credentials;

/// <summary>
/// Credential provider that resolves credentials using the workflow owner's context.
/// Used when a shared workflow is executed with <see cref="Core.Enums.CredentialSharingPolicy.UseOwnerCredentials"/>.
/// 
/// Currently delegates to the same ICredentialService (credentials are looked up by ID
/// regardless of who triggers the execution). This class exists to:
/// 1. Document the intent that owner credentials are being used
/// 2. Provide a hook point if credential service adds per-user scoping later
/// </summary>
public class OwnerCredentialProvider(ICredentialService credentialService, Guid ownerId) : ICredentialProvider
{
    /// <summary>Gets the workflow owner's user ID.</summary>
    public Guid OwnerId => ownerId;

    /// <inheritdoc />
    public async Task<IDictionary<string, string>?> GetCredentialAsync(
        Guid credentialId,
        CancellationToken cancellationToken = default)
    {
        var decrypted = await credentialService.GetDecryptedAsync(credentialId, cancellationToken);
        return decrypted?.Values;
    }
}
