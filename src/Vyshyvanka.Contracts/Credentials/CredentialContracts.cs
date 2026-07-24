using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Contracts.Credentials;

/// <summary>
/// Request to create a new credential.
/// </summary>
public record CreateCredentialRequest
{
    public string Name { get; init; } = string.Empty;
    public CredentialType Type { get; init; }
    public Dictionary<string, string> Data { get; init; } = [];
}

/// <summary>
/// Request to update an existing credential.
/// </summary>
public record UpdateCredentialRequest
{
    public string? Name { get; init; }
    public Dictionary<string, string>? Data { get; init; }
}

/// <summary>
/// Credential response (never includes decrypted values).
/// </summary>
public record CredentialResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public CredentialType Type { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Field keys that have stored values (e.g. ["apiKey", "baseUrl"]).
    /// Values are never included — only the keys, so the UI can show masked indicators.
    /// </summary>
    public List<string>? StoredFields { get; init; }
}
