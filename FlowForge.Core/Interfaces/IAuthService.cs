using FlowForge.Core.Models;

namespace FlowForge.Core.Interfaces;

/// <summary>
/// Service for authentication operations.
/// </summary>
public interface IAuthService
{
    Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<AuthResult> RegisterAsync(string email, string password, string? displayName = null, CancellationToken cancellationToken = default);
    Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}

/// <summary>
/// Result of an authentication operation.
/// </summary>
public record AuthResult
{
    public bool Success { get; init; }
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public User? User { get; init; }
    public string? ErrorMessage { get; init; }
}
