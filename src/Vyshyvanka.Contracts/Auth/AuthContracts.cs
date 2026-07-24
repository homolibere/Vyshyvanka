namespace Vyshyvanka.Contracts.Auth;

/// <summary>
/// Login request.
/// </summary>
public record LoginRequest
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

/// <summary>
/// Login response.
/// </summary>
public record LoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public UserInfo User { get; init; } = null!;
}

/// <summary>
/// User information returned from auth endpoints.
/// </summary>
public record UserInfo
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string Role { get; init; } = string.Empty;
}

/// <summary>
/// Refresh token request.
/// </summary>
public record RefreshRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}

/// <summary>
/// Authentication configuration returned by the API.
/// </summary>
public record AuthConfigResponse
{
    public string Provider { get; init; } = string.Empty;
    public string? Authority { get; init; }
    public string? ClientId { get; init; }
    public bool AllowRegistration { get; init; }
}

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
}

/// <summary>
/// Paginated user list response.
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
/// Request to update a user's profile.
/// </summary>
public record UpdateUserProfileRequest
{
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
}

/// <summary>
/// Request to create a new API key.
/// </summary>
public record CreateApiKeyRequest
{
    public string Name { get; init; } = string.Empty;
    public List<string>? Scopes { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

/// <summary>
/// Response after creating an API key (includes the plain-text key).
/// </summary>
public record CreateApiKeyResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public List<string> Scopes { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

/// <summary>
/// API key response (list/get — no plain-text key).
/// </summary>
public record ApiKeyResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public List<string> Scopes { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime? LastUsedAt { get; init; }
    public bool IsActive { get; init; }
}
