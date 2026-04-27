using System.Security.Claims;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using Microsoft.AspNetCore.Authentication;

namespace FlowForge.Api.Middleware;

/// <summary>
/// Transforms OIDC claims into the local claim format expected by
/// <see cref="FlowForge.Api.Services.CurrentUserService"/> and authorization policies.
/// Runs after the JWT bearer handler validates the external token.
/// </summary>
public class OidcClaimsTransformation(
    IOidcUserProvisioningService provisioningService,
    AuthenticationSettings settings) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (settings.Provider is AuthenticationProvider.BuiltIn)
        {
            return principal;
        }

        if (principal.Identity?.IsAuthenticated != true)
        {
            return principal;
        }

        var user = await provisioningService.ProvisionAsync(principal);
        if (user is null)
        {
            return principal;
        }

        // Build a new identity with the local user claims so the rest of the
        // pipeline (CurrentUserService, authorization policies) works unchanged.
        var localClaims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("sub", user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        if (!string.IsNullOrEmpty(user.DisplayName))
        {
            localClaims.Add(new Claim(ClaimTypes.Name, user.DisplayName));
        }

        var localIdentity = new ClaimsIdentity(localClaims, "FlowForge.Oidc");
        principal.AddIdentity(localIdentity);

        return principal;
    }
}
