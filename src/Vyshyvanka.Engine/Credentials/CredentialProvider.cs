using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Engine.Credentials;

/// <summary>
/// Provides credential retrieval during workflow execution.
/// Credentials are decrypted on-demand and scoped to the execution.
/// </summary>
public class CredentialProvider : ICredentialProvider
{
    private readonly ICredentialService _credentialService;

    /// <summary>
    /// Creates a new credential provider.
    /// </summary>
    /// <param name="credentialService">The credential service for retrieving and decrypting credentials.</param>
    public CredentialProvider(ICredentialService credentialService)
    {
        _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
    }

    /// <inheritdoc />
    public async Task<IDictionary<string, string>?> GetCredentialAsync(
        Guid credentialId, 
        CancellationToken cancellationToken = default)
    {
        var decrypted = await _credentialService.GetDecryptedAsync(credentialId, cancellationToken);
        return decrypted?.Values;
    }
}

/// <summary>
/// A null credential provider that returns no credentials.
/// Used when credentials are not configured or for testing.
/// </summary>
public class NullCredentialProvider : ICredentialProvider
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly NullCredentialProvider Instance = new NullCredentialProvider();

    private NullCredentialProvider() { }

    /// <inheritdoc />
    public Task<IDictionary<string, string>?> GetCredentialAsync(
        Guid credentialId, 
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IDictionary<string, string>?>(null);
    }
}
