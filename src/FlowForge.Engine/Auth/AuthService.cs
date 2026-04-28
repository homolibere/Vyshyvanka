using System.Security.Cryptography;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using FlowForge.Engine.Persistence;

namespace FlowForge.Engine.Auth;

/// <summary>
/// Authentication service for user login and registration.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly UserRepository _userRepositoryInternal;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly JwtSettings _jwtSettings;
    private readonly IAuditLogService? _auditLogService;

    public AuthService(
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        JwtSettings jwtSettings,
        IAuditLogService? auditLogService = null)
    {
        _userRepository = userRepository;
        _userRepositoryInternal = (UserRepository)userRepository;
        _jwtTokenService = jwtTokenService;
        _jwtSettings = jwtSettings;
        _auditLogService = auditLogService;
    }

    public async Task<AuthResult> LoginAsync(string email, string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(password);

        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            await LogAuthenticationAsync(email, false, "Invalid email or password", cancellationToken);
            return new AuthResult { Success = false, ErrorMessage = "Invalid email or password" };
        }

        if (!user.IsActive)
        {
            await LogAuthenticationAsync(email, false, "Account is disabled", cancellationToken);
            return new AuthResult { Success = false, ErrorMessage = "Account is disabled" };
        }

        if (!VerifyPassword(password, user.PasswordHash))
        {
            await LogAuthenticationAsync(email, false, "Invalid email or password", cancellationToken);
            return new AuthResult { Success = false, ErrorMessage = "Invalid email or password" };
        }

        var accessToken = _jwtTokenService.GenerateAccessToken(user);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);

        await _userRepositoryInternal.UpdateRefreshTokenAsync(user.Id, refreshToken, refreshTokenExpiry,
            cancellationToken);

        var updatedUser = user with { LastLoginAt = DateTime.UtcNow };
        await _userRepository.UpdateAsync(updatedUser, cancellationToken);

        await LogAuthenticationAsync(email, true, null, cancellationToken);

        return new AuthResult
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            User = updatedUser
        };
    }

    public async Task<AuthResult> RegisterAsync(string email, string password, string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(password);

        var existingUser = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (existingUser is not null)
        {
            return new AuthResult { Success = false, ErrorMessage = "Email already registered" };
        }

        var passwordHash = HashPassword(password);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = displayName,
            PasswordHash = passwordHash,
            Role = UserRole.Viewer,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var createdUser = await _userRepository.CreateAsync(user, cancellationToken);

        var accessToken = _jwtTokenService.GenerateAccessToken(createdUser);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);

        await _userRepositoryInternal.UpdateRefreshTokenAsync(createdUser.Id, refreshToken, refreshTokenExpiry,
            cancellationToken);

        return new AuthResult
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            User = createdUser
        };
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);

        var user = await _userRepositoryInternal.GetByRefreshTokenAsync(refreshToken, cancellationToken);
        if (user is null)
        {
            return new AuthResult { Success = false, ErrorMessage = "Invalid refresh token" };
        }

        var (storedToken, expiresAt) = await _userRepositoryInternal.GetRefreshTokenAsync(user.Id, cancellationToken);

        if (storedToken != refreshToken || expiresAt < DateTime.UtcNow)
        {
            return new AuthResult { Success = false, ErrorMessage = "Refresh token expired" };
        }

        if (!user.IsActive)
        {
            return new AuthResult { Success = false, ErrorMessage = "Account is disabled" };
        }

        var newAccessToken = _jwtTokenService.GenerateAccessToken(user);
        var newRefreshToken = _jwtTokenService.GenerateRefreshToken();
        var newRefreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);

        await _userRepositoryInternal.UpdateRefreshTokenAsync(user.Id, newRefreshToken, newRefreshTokenExpiry,
            cancellationToken);

        return new AuthResult
        {
            Success = true,
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            User = user
        };
    }

    public Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var result = _jwtTokenService.ValidateToken(token);
        return Task.FromResult(result.IsValid);
    }

    public string HashPassword(string password)
    {
        var salt = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100000, HashAlgorithmName.SHA256, 32);

        var hashBytes = new byte[48];
        Array.Copy(salt, 0, hashBytes, 0, 16);
        Array.Copy(hash, 0, hashBytes, 16, 32);

        return Convert.ToBase64String(hashBytes);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        var hashBytes = Convert.FromBase64String(passwordHash);

        var salt = new byte[16];
        Array.Copy(hashBytes, 0, salt, 0, 16);

        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100000, HashAlgorithmName.SHA256, 32);

        return CryptographicOperations.FixedTimeEquals(hashBytes.AsSpan(16), hash);
    }

    private async Task LogAuthenticationAsync(string email, bool success, string? errorMessage,
        CancellationToken cancellationToken)
    {
        if (_auditLogService is not null)
        {
            await _auditLogService.LogAuthenticationAttemptAsync(email, success, null, null, errorMessage,
                cancellationToken);
        }
    }
}
