using CsCheck;
using Vyshyvanka.Designer.Services;
using Microsoft.Extensions.Configuration;

namespace Vyshyvanka.Tests.Property;

/// <summary>
/// Property-based tests for ApiUrlResolver service discovery.
/// Feature: aspire-orchestration
/// </summary>
public class ApiUrlResolverTests
{
    // Generator for valid URLs (https scheme)
    private static readonly Gen<string> ValidHttpsUrlGen = Gen.Char['a', 'z']
        .Array[3, 15]
        .Select(chars => $"https://{new string(chars)}.example.com");

    // Generator for valid URLs (http scheme)
    private static readonly Gen<string> ValidHttpUrlGen = Gen.Char['a', 'z']
        .Array[3, 15]
        .Select(chars => $"http://{new string(chars)}.example.com");

    // Generator for fallback URLs
    private static readonly Gen<string> FallbackUrlGen = Gen.Char['a', 'z']
        .Array[3, 10]
        .Select(chars => $"https://fallback-{new string(chars)}.local");

    /// <summary>
    /// Feature: aspire-orchestration, Property 4: Designer Uses Discovered API URL
    /// For any valid service discovery configuration, when ResolveApiUrl is called,
    /// the resolver SHALL return the service discovery URL over appsettings or fallback.
    /// HTTPS endpoints take priority over HTTP endpoints.
    /// Validates: Requirements 4.2
    /// </summary>
    [Fact]
    public void ResolveApiUrl_WhenServiceDiscoveryConfigured_ReturnsDiscoveredUrl()
    {
        Gen.Select(ValidHttpsUrlGen, ValidHttpUrlGen, FallbackUrlGen)
            .Sample(tuple =>
            {
                var (httpsUrl, httpUrl, fallbackUrl) = tuple;

                // Test 1: HTTPS service discovery takes priority
                var configWithHttps = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        [ApiUrlResolver.ServiceDiscoveryHttpsKey] = httpsUrl,
                        [ApiUrlResolver.ServiceDiscoveryHttpKey] = httpUrl,
                        [ApiUrlResolver.AppSettingsKey] = "https://appsettings.example.com"
                    })
                    .Build();

                var result = ApiUrlResolver.ResolveApiUrl(configWithHttps, fallbackUrl);
                Assert.Equal(httpsUrl, result);

                // Test 2: HTTP service discovery used when HTTPS not available
                var configWithHttpOnly = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        [ApiUrlResolver.ServiceDiscoveryHttpKey] = httpUrl,
                        [ApiUrlResolver.AppSettingsKey] = "https://appsettings.example.com"
                    })
                    .Build();

                result = ApiUrlResolver.ResolveApiUrl(configWithHttpOnly, fallbackUrl);
                Assert.Equal(httpUrl, result);
            }, iter: 100);
    }


    /// <summary>
    /// Feature: aspire-orchestration, Property 5: Designer Falls Back to AppSettings
    /// For any configuration without service discovery, when ResolveApiUrl is called,
    /// the resolver SHALL return the AppSettings value if present, otherwise the fallback URL.
    /// Validates: Requirements 4.3
    /// </summary>
    [Fact]
    public void ResolveApiUrl_WhenNoServiceDiscovery_FallsBackToAppSettingsOrDefault()
    {
        Gen.Select(ValidHttpsUrlGen, FallbackUrlGen)
            .Sample(tuple =>
            {
                var (appSettingsUrl, fallbackUrl) = tuple;

                // Test 1: Falls back to AppSettings when no service discovery
                var configWithAppSettings = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        [ApiUrlResolver.AppSettingsKey] = appSettingsUrl
                    })
                    .Build();

                var result = ApiUrlResolver.ResolveApiUrl(configWithAppSettings, fallbackUrl);
                Assert.Equal(appSettingsUrl, result);

                // Test 2: Falls back to fallback URL when nothing configured
                var emptyConfig = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>())
                    .Build();

                result = ApiUrlResolver.ResolveApiUrl(emptyConfig, fallbackUrl);
                Assert.Equal(fallbackUrl, result);

                // Test 3: Empty/whitespace values are treated as not configured
                var configWithEmptyValues = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        [ApiUrlResolver.ServiceDiscoveryHttpsKey] = "",
                        [ApiUrlResolver.ServiceDiscoveryHttpKey] = "   ",
                        [ApiUrlResolver.AppSettingsKey] = appSettingsUrl
                    })
                    .Build();

                result = ApiUrlResolver.ResolveApiUrl(configWithEmptyValues, fallbackUrl);
                Assert.Equal(appSettingsUrl, result);
            }, iter: 100);
    }
}
