using FlowForge.Core.Enums;

namespace FlowForge.Core.Models;

/// <summary>
/// Configuration for the active credential storage backend.
/// Bind from the "CredentialStorage" section in appsettings.json.
/// </summary>
public record CredentialStorageSettings
{
    /// <summary>Which storage backend to use.</summary>
    public CredentialStorageProvider Provider { get; init; } = CredentialStorageProvider.BuiltIn;

    /// <summary>
    /// Base URL of the Vault / OpenBao server.
    /// Example: "https://vault.example.com:8200".
    /// Required for HashiCorpVault and OpenBao providers.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Authentication token for the Vault / OpenBao server.
    /// In production, prefer injecting via environment variable VAULT_TOKEN.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// KV v2 mount path. Default: "secret".
    /// </summary>
    public string MountPath { get; init; } = "secret";

    /// <summary>
    /// Path prefix under the mount for FlowForge credentials.
    /// Credentials are stored at {MountPath}/data/{PathPrefix}/{credentialId}.
    /// Default: "flowforge/credentials".
    /// </summary>
    public string PathPrefix { get; init; } = "flowforge/credentials";

    /// <summary>
    /// Skip TLS certificate verification. Only for development.
    /// </summary>
    public bool SkipTlsVerify { get; init; }
}
