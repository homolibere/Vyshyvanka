using System.Security.Claims;
using System.Text.Json;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using Microsoft.Extensions.Logging;

namespace FlowForge.Engine.Auth;

/// <summary>
/// Creates or updates local user records from OIDC claims (Keycloak / Authentik).
/// </summary>
public class OidcUserProvisioningService(
    IUserRepository userRepository,
    AuthenticationSettings settings,
    ILogger<OidcUserProvisioningService> logger) : IOidcUserProvisioningService
{
    public async Task<User?> ProvisionAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? principal.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(subject))
        {
            logger.LogWarning("OIDC token missing subject claim — cannot provision user");
            return null;
        }

        // Try to find existing user by external ID
        var user = await userRepository.GetByExternalIdAsync(subject, cancellationToken);

        if (user is not null)
        {
            // Update last login
            var updated = user with { LastLoginAt = DateTime.UtcNow };
            await userRepository.UpdateAsync(updated, cancellationToken);
            return updated;
        }

        if (!settings.AutoProvisionUsers)
        {
            logger.LogInformation("Auto-provisioning disabled — rejecting unknown OIDC subject {Subject}", subject);
            return null;
        }

        var email = principal.FindFirstValue(ClaimTypes.Email)
                    ?? principal.FindFirstValue("email")
                    ?? $"{subject}@external";

        var displayName = principal.FindFirstValue("name")
                          ?? principal.FindFirstValue(ClaimTypes.Name)
                          ?? principal.FindFirstValue("preferred_username");

        var role = ResolveRole(principal);

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = displayName,
            PasswordHash = string.Empty, // OIDC users don't have local passwords
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            ExternalId = subject,
            AuthenticationProvider = settings.Provider
        };

        var created = await userRepository.CreateAsync(newUser, cancellationToken);
        logger.LogInformation(
            "Provisioned OIDC user {Email} (subject {Subject}) with role {Role}",
            created.Email, subject, role);

        return created;
    }

    private UserRole ResolveRole(ClaimsPrincipal principal)
    {
        var externalRoles = ExtractExternalRoles(principal);

        foreach (var externalRole in externalRoles)
        {
            if (settings.RoleMappings.TryGetValue(externalRole, out var mapped)
                && Enum.TryParse<UserRole>(mapped, ignoreCase: true, out var role))
            {
                return role;
            }
        }

        return settings.DefaultRole;
    }

    private List<string> ExtractExternalRoles(ClaimsPrincipal principal)
    {
        // Keycloak: roles are nested inside a "realm_access" JSON claim
        if (settings.Provider is AuthenticationProvider.Keycloak)
        {
            var realmAccess = principal.FindFirstValue("realm_access");
            if (realmAccess is not null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(realmAccess);
                    if (doc.RootElement.TryGetProperty("roles", out var rolesElement))
                    {
                        return [.. rolesElement.EnumerateArray().Select(r => r.GetString()!)] ;
                    }
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Failed to parse Keycloak realm_access claim");
                }
            }
        }

        // Authentik / generic: flat claim values
        var claimType = settings.RoleClaimType;
        return [.. principal.FindAll(claimType).Select(c => c.Value)];
    }
}
