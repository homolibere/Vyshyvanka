using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Contracts.Credentials;

/// <summary>
/// Request payload to create a new credential. Secret values are encrypted at rest and never returned.
/// </summary>
public record CreateCredentialRequest
{
    /// <summary>Display name of the credential. Required.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The kind of credential, which determines the expected keys in <see cref="Data"/>.</summary>
    public CredentialType Type { get; init; }

    /// <summary>Secret field values keyed by field name (e.g. <c>apiKey</c>, <c>username</c>). Encrypted before storage.</summary>
    public Dictionary<string, string> Data { get; init; } = [];
}

/// <summary>
/// Request payload to update an existing credential. Only supplied fields are changed.
/// </summary>
public record UpdateCredentialRequest
{
    /// <summary>New display name. <c>null</c> leaves the name unchanged.</summary>
    public string? Name { get; init; }

    /// <summary>Replacement secret field values. <c>null</c> leaves the stored secrets unchanged.</summary>
    public Dictionary<string, string>? Data { get; init; }
}

/// <summary>
/// Credential metadata returned by the API. Never includes decrypted secret values.
/// </summary>
public record CredentialResponse
{
    /// <summary>Unique identifier of the credential.</summary>
    public Guid Id { get; init; }

    /// <summary>Display name of the credential.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The kind of credential.</summary>
    public CredentialType Type { get; init; }

    /// <summary>UTC timestamp when the credential was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>UTC timestamp when the credential was last updated.</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Field keys that have stored values (e.g. <c>["apiKey", "baseUrl"]</c>).
    /// Values are never included — only the keys, so the UI can show masked indicators.
    /// <c>null</c> when the caller did not request stored-field information.
    /// </summary>
    public List<string>? StoredFields { get; init; }
}
