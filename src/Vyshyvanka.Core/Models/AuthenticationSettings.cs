using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Core.Models;

/// <summary>
/// Configuration for the active authentication provider.
/// Bind from the "Authentication" section in appsettings.json.
/// </summary>
public record AuthenticationSettings
{
    /// <summary>Which provider to use.</summary>
    public AuthenticationProvider Provider { get; init; } = AuthenticationProvider.BuiltIn;

    /// <summary>OIDC authority URL (required for Keycloak / Authentik).</summary>
    public string? Authority { get; init; }

    /// <summary>OIDC client identifier (required for Keycloak / Authentik).</summary>
    public string? ClientId { get; init; }

    /// <summary>Expected audience in the access token. Falls back to <see cref="ClientId"/> when null.</summary>
    public string? Audience { get; init; }

    /// <summary>Require HTTPS for the OIDC metadata endpoint. Disable only for local development.</summary>
    public bool RequireHttpsMetadata { get; init; } = true;

    /// <summary>
    /// Claim type that carries the user's roles in the external token.
    /// Keycloak default: "realm_access" (nested JSON with a "roles" array).
    /// Authentik default: "groups".
    /// </summary>
    public string RoleClaimType { get; init; } = "role";

    /// <summary>
    /// Maps external role/group names to local <see cref="UserRole"/> values.
    /// Example: { "vyshyvanka-admin": "Admin", "Vyshyvanka Editors": "Editor" }.
    /// </summary>
    public Dictionary<string, string> RoleMappings { get; init; } = new();

    /// <summary>Default role assigned to auto-provisioned OIDC users when no mapping matches.</summary>
    public UserRole DefaultRole { get; init; } = UserRole.Viewer;

    /// <summary>Automatically create a local user record on first OIDC login.</summary>
    public bool AutoProvisionUsers { get; init; } = true;

    /// <summary>LDAP-specific settings (required when Provider is Ldap).</summary>
    public LdapSettings? Ldap { get; init; }
}

/// <summary>
/// LDAP connection and query settings.
/// </summary>
public record LdapSettings
{
    /// <summary>LDAP server hostname or IP address.</summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>LDAP server port. Default: 389 (LDAP) or 636 (LDAPS).</summary>
    public int Port { get; init; } = 389;

    /// <summary>Use SSL/LDAPS for the connection.</summary>
    public bool UseSsl { get; init; }

    /// <summary>Use StartTLS to upgrade a plain connection to TLS.</summary>
    public bool UseStartTls { get; init; }

    /// <summary>
    /// Distinguished name of the service account used to search for users.
    /// Example: "cn=readonly,dc=example,dc=com".
    /// Leave null for anonymous bind (if the server allows it).
    /// </summary>
    public string? BindDn { get; init; }

    /// <summary>Password for the service account bind DN.</summary>
    public string? BindPassword { get; init; }

    /// <summary>
    /// Base DN for user searches.
    /// Example: "ou=users,dc=example,dc=com".
    /// </summary>
    public string SearchBase { get; init; } = string.Empty;

    /// <summary>
    /// LDAP search filter to locate a user. Use {0} as a placeholder for the login identifier.
    /// Example: "(mail={0})" or "(uid={0})" or "(sAMAccountName={0})".
    /// </summary>
    public string UserSearchFilter { get; init; } = "(mail={0})";

    /// <summary>LDAP attribute that contains the user's email address.</summary>
    public string EmailAttribute { get; init; } = "mail";

    /// <summary>LDAP attribute that contains the user's display name.</summary>
    public string DisplayNameAttribute { get; init; } = "cn";

    /// <summary>
    /// LDAP attribute that contains group memberships on the user entry.
    /// Example: "memberOf" (Active Directory / OpenLDAP with memberOf overlay).
    /// </summary>
    public string MemberOfAttribute { get; init; } = "memberOf";

    /// <summary>LDAP attribute on the group entry that contains the group's name.</summary>
    public string GroupNameAttribute { get; init; } = "cn";

    /// <summary>
    /// Maps LDAP group names (CN) to local <see cref="UserRole"/> values.
    /// Example: { "Vyshyvanka-Admins": "Admin", "Vyshyvanka-Editors": "Editor" }.
    /// </summary>
    public Dictionary<string, string> RoleMappings { get; init; } = new();

    /// <summary>Default role when no group mapping matches.</summary>
    public UserRole DefaultRole { get; init; } = UserRole.Viewer;
}
