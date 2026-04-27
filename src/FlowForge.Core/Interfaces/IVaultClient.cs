namespace FlowForge.Core.Interfaces;

/// <summary>
/// Client for reading and writing secrets to a Vault-compatible KV v2 API
/// (HashiCorp Vault or OpenBao).
/// </summary>
public interface IVaultClient
{
    /// <summary>Writes a secret at the given path.</summary>
    Task WriteSecretAsync(string path, Dictionary<string, string> data, CancellationToken cancellationToken = default);

    /// <summary>Reads a secret from the given path. Returns null if not found.</summary>
    Task<Dictionary<string, string>?> ReadSecretAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Deletes a secret (all versions and metadata) at the given path.</summary>
    Task DeleteSecretAsync(string path, CancellationToken cancellationToken = default);
}
