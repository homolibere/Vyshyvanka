namespace FlowForge.Engine.Persistence.Entities;

/// <summary>
/// EF Core entity for API key storage.
/// </summary>
public class ApiKeyEntity
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }
    
    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>SHA-256 hash of the API key.</summary>
    public string KeyHash { get; set; } = string.Empty;
    
    /// <summary>Owner user ID.</summary>
    public Guid UserId { get; set; }
    
    /// <summary>Comma-separated scopes.</summary>
    public string Scopes { get; set; } = string.Empty;
    
    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>Expiration timestamp.</summary>
    public DateTime? ExpiresAt { get; set; }
    
    /// <summary>Last used timestamp.</summary>
    public DateTime? LastUsedAt { get; set; }
    
    /// <summary>Whether the key is active.</summary>
    public bool IsActive { get; set; }
    
    /// <summary>Navigation property to user.</summary>
    public UserEntity? User { get; set; }
}
