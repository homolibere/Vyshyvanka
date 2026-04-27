using FlowForge.Core.Models;

namespace FlowForge.Core.Interfaces;

/// <summary>
/// Authenticates users against an LDAP directory and provisions local user records.
/// </summary>
public interface ILdapAuthenticationService
{
    /// <summary>
    /// Binds to the LDAP server with the given credentials, resolves user
    /// attributes and group memberships, and returns a local <see cref="User"/>.
    /// </summary>
    Task<LdapAuthResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an LDAP authentication attempt.
/// </summary>
public record LdapAuthResult
{
    public bool Success { get; init; }
    public User? User { get; init; }
    public string? ErrorMessage { get; init; }
}
