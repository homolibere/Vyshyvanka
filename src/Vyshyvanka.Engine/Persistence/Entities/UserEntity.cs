using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Engine.Persistence.Entities;

/// <summary>
/// EF Core entity for user storage.
/// </summary>
public class UserEntity
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>User email address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Hashed password.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>User role.</summary>
    public UserRole Role { get; set; }

    /// <summary>Whether the user is active.</summary>
    public bool IsActive { get; set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Last login timestamp.</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Refresh token for JWT authentication.</summary>
    public string? RefreshToken { get; set; }

    /// <summary>Refresh token expiry.</summary>
    public DateTime? RefreshTokenExpiresAt { get; set; }

    /// <summary>External subject identifier from an OIDC provider (null for built-in users).</summary>
    public string? ExternalId { get; set; }

    /// <summary>Which authentication provider owns this user.</summary>
    public Vyshyvanka.Core.Enums.AuthenticationProvider AuthenticationProvider { get; set; }

    /// <summary>API keys owned by this user.</summary>
    public List<ApiKeyEntity> ApiKeys { get; set; } = [];
}
