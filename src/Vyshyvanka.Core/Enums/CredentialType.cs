namespace Vyshyvanka.Core.Enums;

/// <summary>
/// Types of credentials supported by the system.
/// </summary>
public enum CredentialType
{
    /// <summary>API key authentication.</summary>
    ApiKey,
    
    /// <summary>OAuth 2.0 authentication.</summary>
    OAuth2,
    
    /// <summary>Basic authentication (username/password).</summary>
    BasicAuth,
    
    /// <summary>Custom HTTP headers.</summary>
    CustomHeaders
}
