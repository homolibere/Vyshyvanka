using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Core.Models;

/// <summary>
/// Represents an encrypted credential stored in the system.
/// </summary>
public record Credential
{
    /// <summary>Unique identifier for the credential.</summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; }
    
    /// <summary>Display name for the credential.</summary>
    [JsonPropertyName("name")]
    [Required(ErrorMessage = "Credential name is required")]
    [MinLength(1, ErrorMessage = "Credential name cannot be empty")]
    [MaxLength(200, ErrorMessage = "Credential name cannot exceed 200 characters")]
    public string Name { get; init; } = string.Empty;
    
    /// <summary>Type of credential (ApiKey, OAuth2, BasicAuth, CustomHeaders).</summary>
    [JsonPropertyName("type")]
    public CredentialType Type { get; init; }
    
    /// <summary>AES-256 encrypted credential data.</summary>
    [JsonIgnore]
    public byte[] EncryptedData { get; init; } = [];
    
    /// <summary>User who owns this credential.</summary>
    [JsonPropertyName("ownerId")]
    public Guid OwnerId { get; init; }
    
    /// <summary>When the credential was created.</summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }
    
    /// <summary>When the credential was last updated.</summary>
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Represents a decrypted credential available during execution.
/// This type should only exist in memory during node execution.
/// </summary>
public record DecryptedCredential
{
    /// <summary>Credential identifier.</summary>
    public Guid Id { get; init; }
    
    /// <summary>Type of credential.</summary>
    public CredentialType Type { get; init; }
    
    /// <summary>Decrypted credential values (key-value pairs).</summary>
    public Dictionary<string, string> Values { get; init; } = [];
}

/// <summary>
/// Request to create a new credential.
/// </summary>
public record CreateCredentialRequest
{
    /// <summary>Display name for the credential.</summary>
    [Required(ErrorMessage = "Credential name is required")]
    [MinLength(1, ErrorMessage = "Credential name cannot be empty")]
    [MaxLength(200, ErrorMessage = "Credential name cannot exceed 200 characters")]
    public string Name { get; init; } = string.Empty;
    
    /// <summary>Type of credential.</summary>
    public CredentialType Type { get; init; }
    
    /// <summary>Credential values to encrypt and store.</summary>
    [Required(ErrorMessage = "Credential data is required")]
    public Dictionary<string, string> Data { get; init; } = [];
    
    /// <summary>User who owns this credential.</summary>
    public Guid OwnerId { get; init; }
}

/// <summary>
/// Request to update an existing credential.
/// </summary>
public record UpdateCredentialRequest
{
    /// <summary>New display name (optional).</summary>
    [MaxLength(200, ErrorMessage = "Credential name cannot exceed 200 characters")]
    public string? Name { get; init; }
    
    /// <summary>New credential values to encrypt and store (optional).</summary>
    public Dictionary<string, string>? Data { get; init; }
}
