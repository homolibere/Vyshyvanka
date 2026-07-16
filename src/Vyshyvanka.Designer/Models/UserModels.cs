namespace Vyshyvanka.Designer.Models;

/// <summary>
/// User information as returned by the admin API.
/// </summary>
public record AdminUserModel
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
}

/// <summary>
/// Paginated user list response.
/// </summary>
public record UserListResponse
{
    public List<AdminUserModel> Users { get; init; } = [];
    public int TotalCount { get; init; }
}

/// <summary>
/// Request to create a user (admin, BuiltIn provider only).
/// </summary>
public record CreateUserRequest
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string Role { get; init; } = "Editor";
}

/// <summary>
/// Request to update a user's role.
/// </summary>
public record UpdateUserRoleRequest
{
    public string Role { get; init; } = string.Empty;
}

/// <summary>
/// Request to update a user's active status.
/// </summary>
public record UpdateUserStatusRequest
{
    public bool IsActive { get; init; }
}

/// <summary>
/// Authentication configuration returned by the API.
/// </summary>
public record AuthConfigModel
{
    public string Provider { get; init; } = string.Empty;
    public string? Authority { get; init; }
    public string? ClientId { get; init; }
    public bool AllowRegistration { get; init; }
}
