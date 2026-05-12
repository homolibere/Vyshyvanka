using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Core.Models;

/// <summary>
/// Represents a user in the system.
/// </summary>
public record User
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string PasswordHash { get; init; } = string.Empty;
    public UserRole Role { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }

    /// <summary>Number of consecutive failed login attempts.</summary>
    public int FailedLoginAttempts { get; init; }

    /// <summary>UTC time when the account lockout expires. Null means not locked.</summary>
    public DateTime? LockoutEnd { get; init; }

    /// <summary>External subject identifier from an OIDC provider (null for built-in users).</summary>
    public string? ExternalId { get; init; }

    /// <summary>Which authentication provider owns this user.</summary>
    public AuthenticationProvider AuthenticationProvider { get; init; } = AuthenticationProvider.BuiltIn;

    /// <summary>Returns true if the account is currently locked out.</summary>
    public bool IsLockedOut => LockoutEnd.HasValue && LockoutEnd.Value > DateTime.UtcNow;
}
