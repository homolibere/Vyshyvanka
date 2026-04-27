namespace FlowForge.Core.Enums;

/// <summary>
/// Supported authentication providers.
/// </summary>
public enum AuthenticationProvider
{
    /// <summary>Built-in email/password authentication with local JWT tokens.</summary>
    BuiltIn,

    /// <summary>Keycloak OpenID Connect provider.</summary>
    Keycloak,

    /// <summary>Authentik OpenID Connect provider.</summary>
    Authentik,

    /// <summary>LDAP directory authentication with local JWT tokens.</summary>
    Ldap
}
