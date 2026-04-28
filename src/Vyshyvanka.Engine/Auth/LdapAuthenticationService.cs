using System.DirectoryServices.Protocols;
using System.Net;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Microsoft.Extensions.Logging;

namespace Vyshyvanka.Engine.Auth;

/// <summary>
/// Authenticates users against an LDAP directory (Active Directory, OpenLDAP, 389 DS, etc.)
/// and provisions or updates local user records.
/// </summary>
public class LdapAuthenticationService(
    IUserRepository userRepository,
    AuthenticationSettings authSettings,
    ILogger<LdapAuthenticationService> logger) : ILdapAuthenticationService
{
    private LdapSettings Ldap => authSettings.Ldap
        ?? throw new InvalidOperationException("Authentication:Ldap settings are required");

    public async Task<LdapAuthResult> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);

        try
        {
            // Step 1: Search for the user entry using the service account
            var userEntry = SearchUser(username);
            if (userEntry is null)
            {
                logger.LogWarning("LDAP user not found for identifier {Username}", username);
                return new LdapAuthResult { Success = false, ErrorMessage = "Invalid credentials" };
            }

            var userDn = userEntry.DistinguishedName;

            // Step 2: Bind as the user to verify their password
            if (!VerifyBind(userDn, password))
            {
                logger.LogWarning("LDAP bind failed for {UserDn}", userDn);
                return new LdapAuthResult { Success = false, ErrorMessage = "Invalid credentials" };
            }

            // Step 3: Extract attributes
            var email = GetAttribute(userEntry, Ldap.EmailAttribute) ?? username;
            var displayName = GetAttribute(userEntry, Ldap.DisplayNameAttribute);
            var role = ResolveRole(userEntry);

            // Step 4: Provision or update local user
            var user = await ProvisionUserAsync(userDn, email, displayName, role, cancellationToken);

            logger.LogInformation("LDAP authentication succeeded for {Email} (DN: {UserDn})", email, userDn);
            return new LdapAuthResult { Success = true, User = user };
        }
        catch (LdapException ex)
        {
            logger.LogError(ex, "LDAP error during authentication for {Username}", username);
            return new LdapAuthResult { Success = false, ErrorMessage = "Authentication service unavailable" };
        }
    }

    private SearchResultEntry? SearchUser(string username)
    {
        using var connection = CreateConnection();
        BindServiceAccount(connection);

        var filter = string.Format(Ldap.UserSearchFilter, EscapeLdapFilter(username));
        var searchRequest = new SearchRequest(
            Ldap.SearchBase,
            filter,
            SearchScope.Subtree,
            Ldap.EmailAttribute,
            Ldap.DisplayNameAttribute,
            Ldap.MemberOfAttribute);

        var response = (SearchResponse)connection.SendRequest(searchRequest);
        return response.Entries.Count > 0 ? response.Entries[0] : null;
    }

    private bool VerifyBind(string userDn, string password)
    {
        try
        {
            using var connection = CreateConnection();
            connection.Credential = new NetworkCredential(userDn, password);
            connection.Bind();
            return true;
        }
        catch (LdapException ex) when (ex.ErrorCode == 49) // InvalidCredentials
        {
            return false;
        }
    }

    private void BindServiceAccount(LdapConnection connection)
    {
        if (!string.IsNullOrWhiteSpace(Ldap.BindDn))
        {
            connection.Credential = new NetworkCredential(Ldap.BindDn, Ldap.BindPassword);
        }

        connection.Bind();
    }

    private LdapConnection CreateConnection()
    {
        var identifier = new LdapDirectoryIdentifier(Ldap.Host, Ldap.Port);
        var connection = new LdapConnection(identifier)
        {
            AuthType = AuthType.Basic
        };

        connection.SessionOptions.ProtocolVersion = 3;

        if (Ldap.UseSsl)
        {
            connection.SessionOptions.SecureSocketLayer = true;
        }

        if (Ldap.UseStartTls)
        {
            connection.SessionOptions.StartTransportLayerSecurity(null);
        }

        return connection;
    }

    private UserRole ResolveRole(SearchResultEntry entry)
    {
        var memberOf = entry.Attributes[Ldap.MemberOfAttribute];
        if (memberOf is null)
        {
            return Ldap.DefaultRole;
        }

        foreach (var dnObj in memberOf)
        {
            var dn = dnObj is byte[] bytes
                ? System.Text.Encoding.UTF8.GetString(bytes)
                : dnObj?.ToString() ?? string.Empty;

            // Extract the CN from the DN (e.g. "cn=Vyshyvanka-Admins,ou=groups,dc=example,dc=com" → "Vyshyvanka-Admins")
            var groupName = ExtractCn(dn);
            if (groupName is not null
                && Ldap.RoleMappings.TryGetValue(groupName, out var mapped)
                && Enum.TryParse<UserRole>(mapped, ignoreCase: true, out var role))
            {
                return role;
            }
        }

        return Ldap.DefaultRole;
    }

    private async Task<User> ProvisionUserAsync(
        string userDn,
        string email,
        string? displayName,
        UserRole role,
        CancellationToken cancellationToken)
    {
        // Use the DN as the external ID for LDAP users
        var existing = await userRepository.GetByExternalIdAsync(userDn, cancellationToken);

        if (existing is not null)
        {
            var updated = existing with
            {
                Email = email,
                DisplayName = displayName ?? existing.DisplayName,
                Role = role,
                LastLoginAt = DateTime.UtcNow
            };
            return await userRepository.UpdateAsync(updated, cancellationToken);
        }

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = displayName,
            PasswordHash = string.Empty,
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            ExternalId = userDn,
            AuthenticationProvider = AuthenticationProvider.Ldap
        };

        var created = await userRepository.CreateAsync(newUser, cancellationToken);
        logger.LogInformation("Provisioned LDAP user {Email} (DN: {UserDn}) with role {Role}", email, userDn, role);
        return created;
    }

    private static string? GetAttribute(SearchResultEntry entry, string attributeName)
    {
        var attr = entry.Attributes[attributeName];
        if (attr is null || attr.Count == 0)
        {
            return null;
        }

        var value = attr[0];
        return value is byte[] bytes
            ? System.Text.Encoding.UTF8.GetString(bytes)
            : value?.ToString();
    }

    private static string? ExtractCn(string dn)
    {
        // Simple CN extraction from a DN like "cn=GroupName,ou=groups,dc=example,dc=com"
        foreach (var rdn in dn.Split(','))
        {
            var trimmed = rdn.Trim();
            if (trimmed.StartsWith("cn=", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("CN=", StringComparison.Ordinal))
            {
                return trimmed[3..];
            }
        }

        return null;
    }

    private static string EscapeLdapFilter(string input)
    {
        return input
            .Replace("\\", "\\5c")
            .Replace("*", "\\2a")
            .Replace("(", "\\28")
            .Replace(")", "\\29")
            .Replace("\0", "\\00");
    }
}
