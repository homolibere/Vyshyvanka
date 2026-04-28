using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Engine.Auth;

/// <summary>
/// <see cref="IAuthService"/> implementation that delegates credential verification
/// to an LDAP directory while issuing local JWT tokens for session management.
/// Registration is not supported — users are provisioned automatically on first LDAP login.
/// </summary>
public class LdapAuthService(
    ILdapAuthenticationService ldapService,
    IJwtTokenService jwtTokenService,
    JwtSettings jwtSettings,
    IUserRepository userRepository,
    IAuditLogService? auditLogService = null) : IAuthService
{
    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(password);

        var ldapResult = await ldapService.AuthenticateAsync(email, password, cancellationToken);

        if (!ldapResult.Success || ldapResult.User is null)
        {
            await LogAuthAsync(email, false, ldapResult.ErrorMessage, cancellationToken);
            return new AuthResult { Success = false, ErrorMessage = ldapResult.ErrorMessage ?? "Invalid credentials" };
        }

        var user = ldapResult.User;
        var accessToken = jwtTokenService.GenerateAccessToken(user);
        var refreshToken = jwtTokenService.GenerateRefreshToken();
        var refreshExpiry = DateTime.UtcNow.AddDays(jwtSettings.RefreshTokenExpirationDays);

        // Store refresh token on the local user record
        if (userRepository is Engine.Persistence.UserRepository repo)
        {
            await repo.UpdateRefreshTokenAsync(user.Id, refreshToken, refreshExpiry, cancellationToken);
        }

        await LogAuthAsync(email, true, null, cancellationToken);

        return new AuthResult
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(jwtSettings.AccessTokenExpirationMinutes),
            User = user
        };
    }

    public Task<AuthResult> RegisterAsync(string email, string password, string? displayName = null, CancellationToken cancellationToken = default)
    {
        // Self-registration is not supported with LDAP — users are provisioned on first login
        return Task.FromResult(new AuthResult
        {
            Success = false,
            ErrorMessage = "Registration is not available when using LDAP authentication"
        });
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);

        if (userRepository is not Engine.Persistence.UserRepository repo)
        {
            return new AuthResult { Success = false, ErrorMessage = "Token refresh not supported" };
        }

        var user = await repo.GetByRefreshTokenAsync(refreshToken, cancellationToken);
        if (user is null)
        {
            return new AuthResult { Success = false, ErrorMessage = "Invalid refresh token" };
        }

        var (storedToken, expiresAt) = await repo.GetRefreshTokenAsync(user.Id, cancellationToken);
        if (storedToken != refreshToken || expiresAt < DateTime.UtcNow)
        {
            return new AuthResult { Success = false, ErrorMessage = "Refresh token expired" };
        }

        if (!user.IsActive)
        {
            return new AuthResult { Success = false, ErrorMessage = "Account is disabled" };
        }

        var newAccessToken = jwtTokenService.GenerateAccessToken(user);
        var newRefreshToken = jwtTokenService.GenerateRefreshToken();
        var newExpiry = DateTime.UtcNow.AddDays(jwtSettings.RefreshTokenExpirationDays);

        await repo.UpdateRefreshTokenAsync(user.Id, newRefreshToken, newExpiry, cancellationToken);

        return new AuthResult
        {
            Success = true,
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(jwtSettings.AccessTokenExpirationMinutes),
            User = user
        };
    }

    public Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var result = jwtTokenService.ValidateToken(token);
        return Task.FromResult(result.IsValid);
    }

    public string HashPassword(string password) =>
        throw new NotSupportedException("Password hashing is not used with LDAP authentication");

    public bool VerifyPassword(string password, string passwordHash) =>
        throw new NotSupportedException("Password verification is not used with LDAP authentication");

    private async Task LogAuthAsync(string email, bool success, string? error, CancellationToken ct)
    {
        if (auditLogService is not null)
        {
            await auditLogService.LogAuthenticationAttemptAsync(email, success, null, null, error, ct);
        }
    }
}
