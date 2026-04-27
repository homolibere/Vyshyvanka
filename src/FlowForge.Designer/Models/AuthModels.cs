namespace FlowForge.Designer.Models;

/// <summary>
/// Login request sent to the API.
/// </summary>
public record LoginRequest
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

/// <summary>
/// Login response from the API.
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
