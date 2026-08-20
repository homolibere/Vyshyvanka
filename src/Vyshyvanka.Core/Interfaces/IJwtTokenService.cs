using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Core.Interfaces;

/// <summary>
/// Service for JWT token generation and validation.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>Generates a signed access token for the given user.</summary>
    string GenerateAccessToken(User user);

    /// <summary>Generates a new opaque refresh token.</summary>
    string GenerateRefreshToken();

    /// <summary>Validates an access token and returns the extracted claims.</summary>
    TokenValidationResult ValidateToken(string token);

    /// <summary>Extracts the user identifier from a token, or null when it cannot be read.</summary>
    Guid? GetUserIdFromToken(string token);
}

/// <summary>
/// Result of token validation.
/// </summary>
public record TokenValidationResult
{
    /// <summary>Whether the token is valid and not expired.</summary>
    public bool IsValid { get; init; }

    /// <summary>Identifier of the user the token represents. Null when invalid.</summary>
    public Guid? UserId { get; init; }

    /// <summary>Email claim from the token. Null when invalid or absent.</summary>
    public string? Email { get; init; }

    /// <summary>Role claim from the token. Null when invalid or absent.</summary>
    public string? Role { get; init; }

    /// <summary>Error message when validation failed. Null when valid.</summary>
    public string? ErrorMessage { get; init; }
}
