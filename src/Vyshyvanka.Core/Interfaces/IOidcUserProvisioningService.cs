using System.Security.Claims;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Core.Interfaces;

/// <summary>
/// Provisions local user records from OIDC claims on first login.
/// </summary>
public interface IOidcUserProvisioningService
{
    /// <summary>
    /// Finds an existing user by external subject ID, or creates one if
    /// <see cref="AuthenticationSettings.AutoProvisionUsers"/> is enabled.
    /// </summary>
    Task<User?> ProvisionAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}
