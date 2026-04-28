using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Vyshyvanka.Api.Controllers;

/// <summary>
/// Authentication endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController(
    AuthenticationSettings authSettings,
    IServiceProvider serviceProvider) : ControllerBase
{
    /// <summary>
    /// Returns the active authentication provider and OIDC settings so the
    /// client (Blazor WASM) can configure its own auth flow.
    /// </summary>
    [HttpGet("config")]
    [AllowAnonymous]
    public IActionResult GetConfig()
    {
        var isOidc = authSettings.Provider is AuthenticationProvider.Keycloak or AuthenticationProvider.Authentik;

        return Ok(new AuthConfigResponse
        {
            Provider = authSettings.Provider.ToString(),
            Authority = isOidc ? authSettings.Authority : null,
            ClientId = isOidc ? authSettings.ClientId : null
        });
    }

    /// <summary>
    /// Login with email/username and password (built-in and LDAP providers).
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (authSettings.Provider is AuthenticationProvider.Keycloak or AuthenticationProvider.Authentik)
        {
            return BadRequest(new
            {
                code = "UNSUPPORTED",
                message = $"Login endpoint is not available when using {authSettings.Provider} authentication"
            });
        }

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Email and password are required" });
        }

        var authService = serviceProvider.GetRequiredService<IAuthService>();
        var result = await authService.LoginAsync(request.Email, request.Password, cancellationToken);

        if (!result.Success)
        {
            return Unauthorized(new { error = result.ErrorMessage });
        }

        return Ok(ToLoginResponse(result));
    }

    /// <summary>
    /// Register a new user (built-in provider only).
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        if (authSettings.Provider is not AuthenticationProvider.BuiltIn)
        {
            return BadRequest(new
            {
                code = "UNSUPPORTED",
                message = $"Registration is not available when using {authSettings.Provider} authentication"
            });
        }

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Email and password are required" });
        }

        var authService = serviceProvider.GetRequiredService<IAuthService>();
        var result =
            await authService.RegisterAsync(request.Email, request.Password, request.DisplayName, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(ToLoginResponse(result));
    }

    /// <summary>
    /// Refresh access token (built-in and LDAP providers).
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        if (authSettings.Provider is AuthenticationProvider.Keycloak or AuthenticationProvider.Authentik)
        {
            return BadRequest(new
            {
                code = "UNSUPPORTED",
                message = $"Token refresh endpoint is not available when using {authSettings.Provider} authentication"
            });
        }

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new { error = "Refresh token is required" });
        }

        var authService = serviceProvider.GetRequiredService<IAuthService>();
        var result = await authService.RefreshTokenAsync(request.RefreshToken, cancellationToken);

        if (!result.Success)
        {
            return Unauthorized(new { error = result.ErrorMessage });
        }

        return Ok(ToLoginResponse(result));
    }

    private static LoginResponse ToLoginResponse(AuthResult result) => new()
    {
        AccessToken = result.AccessToken!,
        RefreshToken = result.RefreshToken!,
        ExpiresAt = result.ExpiresAt!.Value,
        User = new UserResponse
        {
            Id = result.User!.Id,
            Email = result.User.Email,
            DisplayName = result.User.DisplayName,
            Role = result.User.Role.ToString()
        }
    };
}

public record AuthConfigResponse
{
    public string Provider { get; init; } = string.Empty;
    public string? Authority { get; init; }
    public string? ClientId { get; init; }
}

public record LoginRequest
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public record RegisterRequest
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
}

public record RefreshRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}

public record LoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public UserResponse User { get; init; } = null!;
}

public record UserResponse
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string Role { get; init; } = string.Empty;
}
