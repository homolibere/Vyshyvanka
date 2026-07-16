using System.ComponentModel.DataAnnotations;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Api.Models;

/// <summary>
/// Admin response representing a user in the system.
/// </summary>
public record AdminUserResponse
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string Role { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool IsLockedOut { get; init; }
    public string AuthenticationProvider { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }

    public static AdminUserResponse FromModel(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        Role = user.Role.ToString(),
        IsActive = user.IsActive,
        IsLockedOut = user.IsLockedOut,
        AuthenticationProvider = user.AuthenticationProvider.ToString(),
        CreatedAt = user.CreatedAt,
        LastLoginAt = user.LastLoginAt
    };
}

/// <summary>
/// Paginated list of users.
/// </summary>
public record UserListResponse
{
    public List<AdminUserResponse> Users { get; init; } = [];
    public int TotalCount { get; init; }
}

/// <summary>
/// Request to create a user (admin, BuiltIn provider only).
/// </summary>
public record CreateUserRequest
{
    /// <summary>User email address.</summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [MaxLength(256, ErrorMessage = "Email cannot exceed 256 characters")]
    public string Email { get; init; } = string.Empty;

    /// <summary>Initial password.</summary>
    [Required(ErrorMessage = "Password is required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    [MaxLength(128, ErrorMessage = "Password cannot exceed 128 characters")]
    public string Password { get; init; } = string.Empty;

    /// <summary>Optional display name.</summary>
    [MaxLength(200, ErrorMessage = "Display name cannot exceed 200 characters")]
    public string? DisplayName { get; init; }

    /// <summary>Role to assign (Admin, Editor, Viewer).</summary>
    [Required(ErrorMessage = "Role is required")]
    public string Role { get; init; } = "Editor";
}

/// <summary>
/// Request to update a user's role.
/// </summary>
public record UpdateUserRoleRequest
{
    /// <summary>New role to assign (Admin, Editor, Viewer).</summary>
    [Required(ErrorMessage = "Role is required")]
    public string Role { get; init; } = string.Empty;
}

/// <summary>
/// Request to update a user's active status.
/// </summary>
public record UpdateUserStatusRequest
{
    /// <summary>Whether the user should be active.</summary>
    public bool IsActive { get; init; }
}

/// <summary>
/// Request to update a user's profile (email, display name).
/// </summary>
public record UpdateUserProfileRequest
{
    /// <summary>New email address.</summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [MaxLength(256, ErrorMessage = "Email cannot exceed 256 characters")]
    public string Email { get; init; } = string.Empty;

    /// <summary>New display name.</summary>
    [MaxLength(200, ErrorMessage = "Display name cannot exceed 200 characters")]
    public string? DisplayName { get; init; }
}
