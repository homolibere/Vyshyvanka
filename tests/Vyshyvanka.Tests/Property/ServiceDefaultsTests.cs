using CsCheck;
using Vyshyvanka.ServiceDefaults;
using Vyshyvanka.Tests.Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Vyshyvanka.Tests.Property;

/// <summary>
/// Property-based tests for ServiceDefaults configuration.
/// Feature: aspire-orchestration
/// </summary>
public class ServiceDefaultsTests
{
    // Generator for valid application names (alphanumeric only)
    private static readonly Gen<string> ValidAppNameGen = Gen.Char['a', 'z']
        .Array[3, 20]
        .Select(chars => new string(chars));

    /// <summary>
    /// Feature: aspire-orchestration, Property 1: AddServiceDefaults Configures OpenTelemetry
    /// For any IHostApplicationBuilder, when AddServiceDefaults is called, the resulting
    /// service collection SHALL contain registrations for OpenTelemetry logging, tracing,
    /// and metrics instrumentation.
    /// Validates: Requirements 2.2, 2.3, 2.4
    /// </summary>
    [Fact]
    public void AddServiceDefaults_AlwaysConfiguresOpenTelemetry()
    {
        ValidAppNameGen.Sample(appName =>
        {
            // Arrange
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                ApplicationName = appName,
                EnvironmentName = "Development"
            });

            // Act
            builder.AddServiceDefaults();
            var services = builder.Services;

            // Assert - Verify OpenTelemetry registrations exist
            // Check for MeterProvider (metrics)
            var hasMeterProvider = services.Any(s =>
                s.ServiceType.FullName?.Contains("MeterProvider") == true ||
                s.ImplementationType?.FullName?.Contains("MeterProvider") == true);
            Assert.True(hasMeterProvider, "MeterProvider should be registered for metrics");

            // Check for TracerProvider (tracing)
            var hasTracerProvider = services.Any(s =>
                s.ServiceType.FullName?.Contains("TracerProvider") == true ||
                s.ImplementationType?.FullName?.Contains("TracerProvider") == true);
            Assert.True(hasTracerProvider, "TracerProvider should be registered for tracing");

            // Check for OpenTelemetry services
            var hasOpenTelemetryServices = services.Any(s =>
                s.ServiceType.FullName?.Contains("OpenTelemetry") == true ||
                s.ImplementationType?.FullName?.Contains("OpenTelemetry") == true);
            Assert.True(hasOpenTelemetryServices, "OpenTelemetry services should be registered");
        }, iter: 100);
    }

    /// <summary>
    /// Feature: aspire-orchestration, Property 2: AddServiceDefaults Registers Health Checks
    /// For any IHostApplicationBuilder, when AddServiceDefaults is called, the resulting
    /// service collection SHALL contain health check service registrations.
    /// Validates: Requirements 2.5
    /// </summary>
    [Fact]
    public void AddServiceDefaults_AlwaysRegistersHealthChecks()
    {
        ValidAppNameGen.Sample(appName =>
        {
            // Arrange
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                ApplicationName = appName,
                EnvironmentName = "Development"
            });

            // Act
            builder.AddServiceDefaults();
            var services = builder.Services;

            // Assert - Verify health check registrations exist
            var hasHealthCheckService = services.Any(s =>
                s.ServiceType == typeof(HealthCheckService) ||
                s.ServiceType.FullName?.Contains("HealthCheck") == true);
            Assert.True(hasHealthCheckService, "HealthCheckService should be registered");

            // Build the host and verify health checks can be resolved
            using var host = builder.Build();
            var healthCheckService = host.Services.GetService<HealthCheckService>();
            Assert.NotNull(healthCheckService);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: aspire-orchestration, Property 3: Health Endpoints Are Accessible
    /// For any running Vyshyvanka.Api instance with ServiceDefaults configured,
    /// HTTP GET requests to "/health" and "/alive" SHALL return successful (2xx) responses.
    /// Validates: Requirements 3.4, 3.5
    /// </summary>
    [Fact]
    public async Task HealthEndpoints_AlwaysReturnSuccessfulResponses()
    {
        // Use property-based testing to verify health endpoints across multiple requests
        await Gen.Int[1, 10].SampleAsync(async _ =>
        {
            // Arrange
            await using var factory = new CustomWebApplicationFactory();
            using var client = factory.CreateClient();

            // Act & Assert - /health endpoint
            var healthResponse = await client.GetAsync("/health");
            Assert.True(
                healthResponse.IsSuccessStatusCode,
                $"/health endpoint should return 2xx, got {(int)healthResponse.StatusCode}");

            // Act & Assert - /alive endpoint
            var aliveResponse = await client.GetAsync("/alive");
            Assert.True(
                aliveResponse.IsSuccessStatusCode,
                $"/alive endpoint should return 2xx, got {(int)aliveResponse.StatusCode}");
        }, iter: 100);
    }
}
