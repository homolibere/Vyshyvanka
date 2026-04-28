using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Core.Interfaces;

/// <summary>
/// Service for JWT token generation and validation.
/// </summary>
public interface IJwtTokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    TokenValidationResult ValidateToken(string token);
    Guid? GetUserIdFromToken(string token);
}

/// <summary>
/// Result of token validation.
/// </summary>
public record TokenValidationResult
{
    public bool IsValid { get; init; }
    public Guid? UserId { get; init; }
    public string? Email { get; init; }
    public string? Role { get; init; }
    public string? ErrorMessage { get; init; }
}
