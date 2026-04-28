namespace Vyshyvanka.Core.Enums;

/// <summary>
/// Supported credential storage backends.
/// </summary>
public enum CredentialStorageProvider
{
    /// <summary>Built-in AES-256 encrypted storage in the local database.</summary>
    BuiltIn,

    /// <summary>HashiCorp Vault KV v2 secrets engine.</summary>
    HashiCorpVault,

    /// <summary>OpenBao KV v2 secrets engine (Vault-compatible API).</summary>
    OpenBao
}
