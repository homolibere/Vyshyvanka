namespace Vyshyvanka.Designer.Services;

/// <summary>
/// Resolves the API base URL from service discovery or configuration fallback.
/// </summary>
public static class ApiUrlResolver
{
    /// <summary>
    /// Configuration key for HTTPS service discovery endpoint.
    /// </summary>
    public const string ServiceDiscoveryHttpsKey = "services:api:https:0";

    /// <summary>
    /// Configuration key for HTTP service discovery endpoint.
    /// </summary>
    public const string ServiceDiscoveryHttpKey = "services:api:http:0";

    /// <summary>
    /// Configuration key for API base address in appsettings.
    /// </summary>
    public const string AppSettingsKey = "ApiBaseAddress";

    /// <summary>
    /// Resolves the API URL from configuration with the following priority:
    /// 1. Service discovery HTTPS endpoint (services:api:https:0)
    /// 2. Service discovery HTTP endpoint (services:api:http:0)
    /// 3. AppSettings value (ApiBaseAddress)
    /// 4. Fallback URL (typically the host environment base address)
    /// </summary>
    /// <param name="configuration">The configuration to read from.</param>
    /// <param name="fallbackUrl">The fallback URL if no configuration is found.</param>
    /// <returns>The resolved API URL.</returns>
    public static string ResolveApiUrl(IConfiguration configuration, string fallbackUrl)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(fallbackUrl);

        // Try service discovery HTTPS first
        var url = configuration[ServiceDiscoveryHttpsKey];
        if (!string.IsNullOrWhiteSpace(url))
            return url;

        // Try service discovery HTTP
        url = configuration[ServiceDiscoveryHttpKey];
        if (!string.IsNullOrWhiteSpace(url))
            return url;

        // Try appsettings value
        url = configuration[AppSettingsKey];
        if (!string.IsNullOrWhiteSpace(url))
            return url;

        // Use fallback
        return fallbackUrl;
    }
}
