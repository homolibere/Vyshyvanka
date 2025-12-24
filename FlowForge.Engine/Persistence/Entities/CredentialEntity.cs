using FlowForge.Core.Enums;

namespace FlowForge.Engine.Persistence.Entities;

/// <summary>
/// EF Core entity for credential storage.
/// </summary>
public class CredentialEntity
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }
    
    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Credential type.</summary>
    public CredentialType Type { get; set; }
    
    /// <summary>AES-256 encrypted credential data.</summary>
    public byte[] EncryptedData { get; set; } = [];
    
    /// <summary>Owner user ID.</summary>
    public Guid OwnerId { get; set; }
    
    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>Last update timestamp.</summary>
    public DateTime UpdatedAt { get; set; }
}
