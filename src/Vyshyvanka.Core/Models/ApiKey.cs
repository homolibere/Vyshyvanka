namespace Vyshyvanka.Core.Models;

/// <summary>
/// Represents an API key for programmatic access.
/// </summary>
public record ApiKey
{
    /// <summary>Unique identifier of the API key.</summary>
    public Guid Id { get; init; }

    /// <summary>Human-readable label identifying the key in the UI.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Hash of the key value (never the plain text). Used to validate presented keys.</summary>
    public string KeyHash { get; init; } = string.Empty;

    /// <summary>Identifier of the user that owns the key.</summary>
    public Guid UserId { get; init; }

    /// <summary>Scopes limiting what the key may do. Empty means the owner's default scopes.</summary>
    public List<string> Scopes { get; init; } = [];

    /// <summary>UTC timestamp when the key was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>UTC expiry time. Null when the key never expires.</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>UTC timestamp when the key was last used to authenticate. Null if never used.</summary>
    public DateTime? LastUsedAt { get; init; }

    /// <summary>Whether the key is currently active and accepted for authentication.</summary>
    public bool IsActive { get; init; } = true;
}
