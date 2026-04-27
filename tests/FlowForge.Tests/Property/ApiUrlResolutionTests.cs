using CsCheck;
using FlowForge.Designer.Services;
using Microsoft.Extensions.Configuration;

namespace FlowForge.Tests.Property;

/// <summary>
/// Property-based tests for API URL resolution in the Designer.
/// Feature: aspire-orchestration
/// </summary>
public class ApiUrlResolutionTests
{
    // Generator for valid URLs
    private static readonly Gen<string> ValidUrlGen = Gen.OneOf(
        Gen.Const("https://localhost:5001"),
        Gen.Const("http://localhost:5000"),
        Gen.Const("https://api.example.com"),
        Gen.Const("http://api.internal:8080"),
        Gen.Int[1000, 9999].Select(port => $"https://localhost:{port}"),
        Gen.Int[1000, 9999].Select(port => $"http://localhost:{port}")
    );

    // Generator for fallback URLs
    private static readonly Gen<string> FallbackUrlGen = Gen.OneOf(
        Gen.Const("https://fallback.local"),
        Gen.Const("http://localhost:3000"),
        Gen.Int[1000, 9999].Select(port => $"https://fallback:{port}")
    );

    /// <summary>
    /// Feature: aspire-orchestration, Property 4: Designer Uses Discovered API URL
    /// For any Designer configuration where service discovery provides an API URL,
    /// the resolved URL SHALL be the discovered URL.
    /// Validates: Requirements 4.2
    /// </summary>
    [Fact]
    public void ResolveApiUrl_WhenServiceDiscoveryHttpsProvided_ReturnsDiscoveredUrl()
    {
        ValidUrlGen.Select(url => (discoveredUrl: url, fallback: "https://fallback.local"))
            .Sample(data =>
            {
                // Arrange - Configuration with service discovery HTTPS URL
                var configData = new Dictionary<string, string?>
                {
                    [ApiUrlResolver.ServiceDiscoveryHttpsKey] = data.discoveredUrl
                };
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(configData)
                    .Build();

                // Act
                var result = ApiUrlResolver.ResolveApiUrl(configuration, data.fallback);

                // Assert - Should use the discovered HTTPS URL
                Assert.Equal(data.discoveredUrl, result);
            }, iter: 100);
    }

    /// <summary>
    /// Feature: aspire-orchestration, Property 4: Designer Uses Discovered API URL (HTTP variant)
    /// For any Designer configuration where service discovery provides an HTTP API URL
    /// (and no HTTPS), the resolved URL SHALL be the discovered HTTP URL.
    /// Validates: Requirements 4.2
    /// </summary>
    [Fact]
    public void ResolveApiUrl_WhenServiceDiscoveryHttpProvided_ReturnsDiscoveredUrl()
    {
        ValidUrlGen.Select(url => (discoveredUrl: url, fallback: "https://fallback.local"))
            .Sample(data =>
            {
                // Arrange - Configuration with service discovery HTTP URL only (no HTTPS)
                var configData = new Dictionary<string, string?>
                {
                    [ApiUrlResolver.ServiceDiscoveryHttpKey] = data.discoveredUrl
                };
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(configData)
                    .Build();

                // Act
                var result = ApiUrlResolver.ResolveApiUrl(configuration, data.fallback);

                // Assert - Should use the discovered HTTP URL
                Assert.Equal(data.discoveredUrl, result);
            }, iter: 100);
    }

    /// <summary>
    /// Feature: aspire-orchestration, Property 4: Designer Uses Discovered API URL (HTTPS priority)
    /// For any Designer configuration where both HTTPS and HTTP service discovery URLs are provided,
    /// the resolved URL SHALL be the HTTPS URL (higher priority).
    /// Validates: Requirements 4.2
    /// </summary>
    [Fact]
    public void ResolveApiUrl_WhenBothHttpsAndHttpProvided_PrefersHttps()
    {
        Gen.Select(ValidUrlGen, ValidUrlGen, FallbackUrlGen)
            .Sample(data =>
            {
                var (httpsUrl, httpUrl, fallback) = data;

                // Arrange - Configuration with both HTTPS and HTTP URLs
                var configData = new Dictionary<string, string?>
                {
                    [ApiUrlResolver.ServiceDiscoveryHttpsKey] = httpsUrl,
                    [ApiUrlResolver.ServiceDiscoveryHttpKey] = httpUrl
                };
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(configData)
                    .Build();

                // Act
                var result = ApiUrlResolver.ResolveApiUrl(configuration, fallback);

                // Assert - Should prefer HTTPS over HTTP
                Assert.Equal(httpsUrl, result);
            }, iter: 100);
    }

    /// <summary>
    /// Feature: aspire-orchestration, Property 5: Designer Falls Back to AppSettings
    /// For any Designer configuration where service discovery does NOT provide an API URL,
    /// the resolved URL SHALL be the value from appsettings configuration.
    /// Validates: Requirements 4.3
    /// </summary>
    [Fact]
    public void ResolveApiUrl_WhenNoServiceDiscovery_FallsBackToAppSettings()
    {
        Gen.Select(ValidUrlGen, FallbackUrlGen)
            .Sample(data =>
            {
                var (appSettingsUrl, fallback) = data;

                // Arrange - Configuration with only AppSettings URL (no service discovery)
                var configData = new Dictionary<string, string?>
                {
                    [ApiUrlResolver.AppSettingsKey] = appSettingsUrl
                };
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(configData)
                    .Build();

                // Act
                var result = ApiUrlResolver.ResolveApiUrl(configuration, fallback);

                // Assert - Should use the AppSettings URL
                Assert.Equal(appSettingsUrl, result);
            }, iter: 100);
    }

    /// <summary>
    /// Feature: aspire-orchestration, Property 5: Designer Falls Back to AppSettings (final fallback)
    /// For any Designer configuration where neither service discovery nor appsettings provide a URL,
    /// the resolved URL SHALL be the fallback URL.
    /// Validates: Requirements 4.3
    /// </summary>
    [Fact]
    public void ResolveApiUrl_WhenNoConfiguration_UsesFallbackUrl()
    {
        FallbackUrlGen.Sample(fallback =>
        {
            // Arrange - Empty configuration
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            // Act
            var result = ApiUrlResolver.ResolveApiUrl(configuration, fallback);

            // Assert - Should use the fallback URL
            Assert.Equal(fallback, result);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: aspire-orchestration, Property 5: Designer Falls Back to AppSettings (priority order)
    /// For any Designer configuration, the URL resolution SHALL follow the priority:
    /// 1. Service discovery HTTPS
    /// 2. Service discovery HTTP
    /// 3. AppSettings
    /// 4. Fallback
    /// Validates: Requirements 4.3
    /// </summary>
    [Fact]
    public void ResolveApiUrl_ServiceDiscoveryTakesPriorityOverAppSettings()
    {
        Gen.Select(ValidUrlGen, ValidUrlGen, FallbackUrlGen)
            .Sample(data =>
            {
                var (discoveredUrl, appSettingsUrl, fallback) = data;

                // Arrange - Configuration with both service discovery and AppSettings
                var configData = new Dictionary<string, string?>
                {
                    [ApiUrlResolver.ServiceDiscoveryHttpsKey] = discoveredUrl,
                    [ApiUrlResolver.AppSettingsKey] = appSettingsUrl
                };
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(configData)
                    .Build();

                // Act
                var result = ApiUrlResolver.ResolveApiUrl(configuration, fallback);

                // Assert - Service discovery should take priority over AppSettings
                Assert.Equal(discoveredUrl, result);
            }, iter: 100);
    }
}
