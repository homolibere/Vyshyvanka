using CsCheck;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Engine.Auth;
using Xunit;

namespace Vyshyvanka.Tests.Property;

/// <summary>
/// Property-based tests for API authentication enforcement.
/// Feature: vyshyvanka, Property 15: API Authentication Enforcement
/// Validates: Requirements 8.5
/// </summary>
public class ApiAuthenticationTests
{
    private readonly JwtSettings _jwtSettings = new()
    {
        SecretKey = "TestSecretKeyForPropertyBasedTesting123456789012345678901234567890",
        Issuer = "Vyshyvanka",
        Audience = "Vyshyvanka",
        AccessTokenExpirationMinutes = 60,
        RefreshTokenExpirationDays = 7
    };

    /// <summary>
    /// Property 15: API Authentication Enforcement - Invalid JWT tokens are rejected
    /// For any random string that is not a valid JWT token, the System SHALL reject 
    /// the token with an authentication error.
    /// </summary>
    [Fact]
    public void JwtValidation_RejectsInvalidTokens()
    {
        var jwtService = new JwtTokenService(_jwtSettings);

        // Generate random strings that are not valid JWT tokens
        var gen = Gen.String[0, 500];

        gen.Sample(randomToken =>
        {
            // Act: Validate the random string as a JWT token
            var result = jwtService.ValidateToken(randomToken);

            // Assert: Invalid tokens should be rejected
            Assert.False(result.IsValid, $"Random string should not be a valid JWT token: '{randomToken}'");
            Assert.NotNull(result.ErrorMessage);
        }, iter: 100);
    }

    /// <summary>
    /// Property 15: API Authentication Enforcement - Empty/null tokens are rejected
    /// For any empty, null, or whitespace-only authentication credential, 
    /// the System SHALL reject the request.
    /// </summary>
    [Fact]
    public void JwtValidation_RejectsEmptyOrWhitespaceTokens()
    {
        var jwtService = new JwtTokenService(_jwtSettings);

        // Generate whitespace-only strings
        var gen = Gen.Char.Array[0, 20]
            .Select(chars => new string(chars.Select(c => char.IsWhiteSpace(c) ? c : ' ').ToArray()));

        gen.Sample(whitespaceToken =>
        {
            // Act: Validate empty/whitespace token
            var result = jwtService.ValidateToken(whitespaceToken);

            // Assert: Empty/whitespace tokens should be rejected
            Assert.False(result.IsValid, "Empty or whitespace token should not be valid");
        }, iter: 100);
    }

    /// <summary>
    /// Property 15: API Authentication Enforcement - Tampered tokens are rejected
    /// For any valid JWT token that has been modified, the System SHALL reject 
    /// the token with an authentication error.
    /// </summary>
    [Fact]
    public void JwtValidation_RejectsTamperedTokens()
    {
        var jwtService = new JwtTokenService(_jwtSettings);

        // Generate random users and tamper positions
        var gen = Gen.Select(
            Gen.Guid,
            Gen.String[5, 50].Where(s => !string.IsNullOrWhiteSpace(s) && s.Contains('@') == false),
            Gen.Int[0, 2],  // Which part to tamper (header, payload, signature)
            Gen.Int[1, 10]  // How many characters to change
        );

        gen.Sample((userId, emailBase, tamperPart, tamperCount) =>
        {
            // Arrange: Create a valid user and generate a valid token
            var user = new User
            {
                Id = userId,
                Email = $"{emailBase}@test.com",
                DisplayName = "Test User",
                Role = UserRole.Editor,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var validToken = jwtService.GenerateAccessToken(user);
            var parts = validToken.Split('.');
            
            if (parts.Length != 3)
            {
                return; // Skip if token format is unexpected
            }

            // Tamper with the token
            var tamperedParts = parts.ToArray();
            var partToTamper = tamperPart % 3;
            
            if (tamperedParts[partToTamper].Length > tamperCount)
            {
                var chars = tamperedParts[partToTamper].ToCharArray();
                for (int i = 0; i < Math.Min(tamperCount, chars.Length); i++)
                {
                    chars[i] = chars[i] == 'a' ? 'b' : 'a';
                }
                tamperedParts[partToTamper] = new string(chars);
            }

            var tamperedToken = string.Join(".", tamperedParts);

            // Act: Validate the tampered token
            var result = jwtService.ValidateToken(tamperedToken);

            // Assert: Tampered tokens should be rejected
            Assert.False(result.IsValid, "Tampered token should not be valid");
        }, iter: 100);
    }

    /// <summary>
    /// Property 15: API Authentication Enforcement - Valid tokens are accepted
    /// For any valid user, a properly generated JWT token SHALL be accepted.
    /// This is the inverse property to ensure our rejection tests are meaningful.
    /// </summary>
    [Fact]
    public void JwtValidation_AcceptsValidTokens()
    {
        var jwtService = new JwtTokenService(_jwtSettings);

        // Generate random valid users
        var gen = Gen.Select(
            Gen.Guid,
            Gen.String[3, 30].Where(s => !string.IsNullOrWhiteSpace(s)),
            Gen.OneOfConst(UserRole.Viewer, UserRole.Editor, UserRole.Admin)
        );

        gen.Sample((userId, emailPrefix, role) =>
        {
            // Arrange: Create a valid user
            var user = new User
            {
                Id = userId,
                Email = $"{emailPrefix.Replace(" ", "")}@test.com",
                DisplayName = "Test User",
                Role = role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // Act: Generate and validate token
            var token = jwtService.GenerateAccessToken(user);
            var result = jwtService.ValidateToken(token);

            // Assert: Valid tokens should be accepted
            Assert.True(result.IsValid, $"Valid token should be accepted. Error: {result.ErrorMessage}");
            Assert.Equal(userId, result.UserId);
            Assert.Equal(role.ToString(), result.Role);
        }, iter: 100);
    }

    /// <summary>
    /// Property 15: API Authentication Enforcement - Token user ID extraction
    /// For any valid token, GetUserIdFromToken SHALL return the correct user ID,
    /// and for any invalid token, it SHALL return null.
    /// </summary>
    [Fact]
    public void JwtValidation_UserIdExtractionConsistency()
    {
        var jwtService = new JwtTokenService(_jwtSettings);

        var gen = Gen.Select(
            Gen.Guid,
            Gen.String[3, 20].Where(s => !string.IsNullOrWhiteSpace(s)),
            Gen.String[0, 100]  // Random invalid token
        );

        gen.Sample((userId, emailPrefix, randomString) =>
        {
            // Arrange: Create a valid user and token
            var user = new User
            {
                Id = userId,
                Email = $"{emailPrefix.Replace(" ", "")}@test.com",
                Role = UserRole.Editor,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var validToken = jwtService.GenerateAccessToken(user);

            // Act & Assert: Valid token returns correct user ID
            var extractedUserId = jwtService.GetUserIdFromToken(validToken);
            Assert.Equal(userId, extractedUserId);

            // Act & Assert: Invalid token returns null
            var invalidUserId = jwtService.GetUserIdFromToken(randomString);
            Assert.Null(invalidUserId);
        }, iter: 100);
    }

    /// <summary>
    /// Property 15: API Authentication Enforcement - Tokens with wrong signature key are rejected
    /// For any token generated with a different secret key, the System SHALL reject it.
    /// </summary>
    [Fact]
    public void JwtValidation_RejectsTokensWithWrongSignatureKey()
    {
        var validJwtService = new JwtTokenService(_jwtSettings);
        
        // Generate random secret keys
        var gen = Gen.Select(
            Gen.Guid,
            Gen.String[3, 20].Where(s => !string.IsNullOrWhiteSpace(s)),
            Gen.String[64, 100].Where(s => !string.IsNullOrWhiteSpace(s) && s != _jwtSettings.SecretKey)
        );

        gen.Sample((userId, emailPrefix, differentSecretKey) =>
        {
            // Arrange: Create a service with a different secret key
            var differentSettings = new JwtSettings
            {
                SecretKey = differentSecretKey,
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                AccessTokenExpirationMinutes = _jwtSettings.AccessTokenExpirationMinutes
            };
            var differentJwtService = new JwtTokenService(differentSettings);

            var user = new User
            {
                Id = userId,
                Email = $"{emailPrefix.Replace(" ", "")}@test.com",
                Role = UserRole.Editor,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // Generate token with different key
            var tokenFromDifferentKey = differentJwtService.GenerateAccessToken(user);

            // Act: Validate with the original service
            var result = validJwtService.ValidateToken(tokenFromDifferentKey);

            // Assert: Token signed with different key should be rejected
            Assert.False(result.IsValid, "Token signed with different key should be rejected");
        }, iter: 100);
    }

    /// <summary>
    /// Property 15: API Authentication Enforcement - Tokens with wrong issuer are rejected
    /// For any token generated with a different issuer, the System SHALL reject it.
    /// </summary>
    [Fact]
    public void JwtValidation_RejectsTokensWithWrongIssuer()
    {
        var validJwtService = new JwtTokenService(_jwtSettings);

        var gen = Gen.Select(
            Gen.Guid,
            Gen.String[3, 20].Where(s => !string.IsNullOrWhiteSpace(s)),
            Gen.String[5, 30].Where(s => !string.IsNullOrWhiteSpace(s) && s != _jwtSettings.Issuer)
        );

        gen.Sample((userId, emailPrefix, differentIssuer) =>
        {
            // Arrange: Create a service with a different issuer
            var differentSettings = new JwtSettings
            {
                SecretKey = _jwtSettings.SecretKey,
                Issuer = differentIssuer,
                Audience = _jwtSettings.Audience,
                AccessTokenExpirationMinutes = _jwtSettings.AccessTokenExpirationMinutes
            };
            var differentJwtService = new JwtTokenService(differentSettings);

            var user = new User
            {
                Id = userId,
                Email = $"{emailPrefix.Replace(" ", "")}@test.com",
                Role = UserRole.Editor,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // Generate token with different issuer
            var tokenFromDifferentIssuer = differentJwtService.GenerateAccessToken(user);

            // Act: Validate with the original service
            var result = validJwtService.ValidateToken(tokenFromDifferentIssuer);

            // Assert: Token with different issuer should be rejected
            Assert.False(result.IsValid, "Token with different issuer should be rejected");
        }, iter: 100);
    }

    /// <summary>
    /// Property 15: API Authentication Enforcement - Tokens with wrong audience are rejected
    /// For any token generated with a different audience, the System SHALL reject it.
    /// </summary>
    [Fact]
    public void JwtValidation_RejectsTokensWithWrongAudience()
    {
        var validJwtService = new JwtTokenService(_jwtSettings);

        var gen = Gen.Select(
            Gen.Guid,
            Gen.String[3, 20].Where(s => !string.IsNullOrWhiteSpace(s)),
            Gen.String[5, 30].Where(s => !string.IsNullOrWhiteSpace(s) && s != _jwtSettings.Audience)
        );

        gen.Sample((userId, emailPrefix, differentAudience) =>
        {
            // Arrange: Create a service with a different audience
            var differentSettings = new JwtSettings
            {
                SecretKey = _jwtSettings.SecretKey,
                Issuer = _jwtSettings.Issuer,
                Audience = differentAudience,
                AccessTokenExpirationMinutes = _jwtSettings.AccessTokenExpirationMinutes
            };
            var differentJwtService = new JwtTokenService(differentSettings);

            var user = new User
            {
                Id = userId,
                Email = $"{emailPrefix.Replace(" ", "")}@test.com",
                Role = UserRole.Editor,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // Generate token with different audience
            var tokenFromDifferentAudience = differentJwtService.GenerateAccessToken(user);

            // Act: Validate with the original service
            var result = validJwtService.ValidateToken(tokenFromDifferentAudience);

            // Assert: Token with different audience should be rejected
            Assert.False(result.IsValid, "Token with different audience should be rejected");
        }, iter: 100);
    }
}
