using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using Microsoft.IdentityModel.Tokens;
using CoreTokenValidationResult = FlowForge.Core.Interfaces.TokenValidationResult;

namespace FlowForge.Engine.Auth;

/// <summary>
/// JWT token generation and validation service.
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;
    private readonly SymmetricSecurityKey _signingKey;

    public JwtTokenService(JwtSettings settings)
    {
        _settings = settings;
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
    }

    public string GenerateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrEmpty(user.DisplayName))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Name, user.DisplayName));
        }

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public CoreTokenValidationResult ValidateToken(string token)
    {
        // Early validation for null/empty tokens
        if (string.IsNullOrWhiteSpace(token))
        {
            return new CoreTokenValidationResult { IsValid = false, ErrorMessage = "Token is required" };
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _signingKey,
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            
            if (validatedToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return new CoreTokenValidationResult { IsValid = false, ErrorMessage = "Invalid token algorithm" };
            }

            var userIdClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub) 
                ?? principal.FindFirst(ClaimTypes.NameIdentifier);
            var emailClaim = principal.FindFirst(JwtRegisteredClaimNames.Email) 
                ?? principal.FindFirst(ClaimTypes.Email);
            var roleClaim = principal.FindFirst(ClaimTypes.Role);

            if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return new CoreTokenValidationResult { IsValid = false, ErrorMessage = "Invalid user ID in token" };
            }

            return new CoreTokenValidationResult
            {
                IsValid = true,
                UserId = userId,
                Email = emailClaim?.Value,
                Role = roleClaim?.Value
            };
        }
        catch (SecurityTokenExpiredException)
        {
            return new CoreTokenValidationResult { IsValid = false, ErrorMessage = "Token has expired" };
        }
        catch (SecurityTokenException ex)
        {
            return new CoreTokenValidationResult { IsValid = false, ErrorMessage = ex.Message };
        }
        catch (ArgumentException ex)
        {
            return new CoreTokenValidationResult { IsValid = false, ErrorMessage = ex.Message };
        }
        catch (FormatException)
        {
            return new CoreTokenValidationResult { IsValid = false, ErrorMessage = "Invalid token format" };
        }
        catch (Exception ex)
        {
            // Catch any other unexpected exceptions to ensure we never throw
            return new CoreTokenValidationResult { IsValid = false, ErrorMessage = $"Token validation failed: {ex.Message}" };
        }
    }

    public Guid? GetUserIdFromToken(string token)
    {
        var result = ValidateToken(token);
        return result.IsValid ? result.UserId : null;
    }
}

/// <summary>
/// JWT configuration settings.
/// </summary>
public class JwtSettings
{
    public string SecretKey { get; init; } = string.Empty;
    public string Issuer { get; init; } = "FlowForge";
    public string Audience { get; init; } = "FlowForge";
    public int AccessTokenExpirationMinutes { get; init; } = 60;
    public int RefreshTokenExpirationDays { get; init; } = 7;
}
