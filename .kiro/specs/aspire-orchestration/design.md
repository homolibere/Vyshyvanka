# Design Document: Aspire Orchestration

## Overview

This design document describes the architecture and implementation approach for adding .NET Aspire orchestration to FlowForge. The solution introduces two new projects (FlowForge.AppHost and FlowForge.ServiceDefaults) that provide unified orchestration, service discovery, and observability for the existing FlowForge.Api and FlowForge.Designer services.

## Architecture

The Aspire integration follows a hub-and-spoke model where the AppHost acts as the central orchestrator:

```
┌─────────────────────────────────────────────────────────────────┐
│                     FlowForge.AppHost                           │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │              DistributedApplication                      │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐  │   │
│  │  │   api       │  │  designer   │  │  postgres (opt) │  │   │
│  │  │  (Project)  │  │  (Project)  │  │   (Container)   │  │   │
│  │  └──────┬──────┘  └──────┬──────┘  └────────┬────────┘  │   │
│  │         │                │                   │           │   │
│  │         └────────────────┼───────────────────┘           │   │
│  │                          │                               │   │
│  │              Service Discovery & Config Injection        │   │
│  └─────────────────────────────────────────────────────────┘   │
│                              │                                  │
│                    Aspire Dashboard                             │
└─────────────────────────────────────────────────────────────────┘
                               │
        ┌──────────────────────┼──────────────────────┐
        │                      │                      │
        ▼                      ▼                      ▼
┌───────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ FlowForge.Api │    │FlowForge.Designer│   │ PostgreSQL DB   │
│               │    │                  │    │   (optional)    │
│ References:   │    │ Receives:        │    │                 │
│ ServiceDefaults│   │ - API URL via    │    │ Connection      │
│               │    │   env vars       │    │ string injected │
└───────────────┘    └─────────────────┘    └─────────────────┘
```

## Components and Interfaces

### FlowForge.AppHost

The orchestration project that defines the distributed application topology.

```csharp
// Program.cs
var builder = DistributedApplication.CreateBuilder(args);

// Add API service
var api = builder.AddProject<Projects.FlowForge_Api>("api");

// Add Designer service with reference to API
var designer = builder.AddProject<Projects.FlowForge_Designer>("designer")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
```

**Project File Structure:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="Aspire.AppHost.Sdk" Version="9.1.0" />
  
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <IsAspireHost>true</IsAspireHost>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\FlowForge.Api\FlowForge.Api.csproj" />
    <ProjectReference Include="..\FlowForge.Designer\FlowForge.Designer.csproj" />
  </ItemGroup>
</Project>
```

### FlowForge.ServiceDefaults

Shared configuration project providing telemetry, health checks, and resilience patterns.

```csharp
// Extensions.cs
namespace FlowForge.ServiceDefaults;

public static class Extensions
{
    public static IHostApplicationBuilder AddServiceDefaults(
        this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });
        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = _ => false
        });
        return app;
    }

    private static IHostApplicationBuilder ConfigureOpenTelemetry(
        this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();
        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters(
        this IHostApplicationBuilder builder)
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }
        return builder;
    }

    private static IHostApplicationBuilder AddDefaultHealthChecks(
        this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy());
        return builder;
    }
}
```

**Project File Structure:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsAspireSharedProject>true</IsAspireSharedProject>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" />
    <PackageReference Include="Microsoft.Extensions.ServiceDiscovery" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" />
  </ItemGroup>
</Project>
```

### FlowForge.Api Integration

Modifications to integrate with Aspire ServiceDefaults:

```csharp
// Program.cs modifications
var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// ... existing service registrations ...

var app = builder.Build();

// Map Aspire health endpoints
app.MapDefaultEndpoints();

// ... existing middleware and endpoints ...

app.Run();
```

### FlowForge.Designer Integration

The Designer receives the API URL through environment variables injected by Aspire:

```csharp
// Program.cs modifications
var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Get API URL from service discovery or fallback to config
var apiUrl = builder.Configuration["services:api:https:0"] 
    ?? builder.Configuration["services:api:http:0"]
    ?? builder.Configuration["ApiBaseUrl"]
    ?? "https://localhost:5001";

builder.Services.AddScoped(sp => new HttpClient 
{ 
    BaseAddress = new Uri(apiUrl) 
});
```

## Data Models

No new domain models are introduced. The Aspire integration uses configuration and environment variables for service coordination.

### Configuration Model

```csharp
// AppHost configuration via environment/launch settings
public record AspireConfiguration
{
    public bool UsePostgres { get; init; }
    public string? PostgresConnectionString { get; init; }
    public int ApiPort { get; init; } = 5001;
    public int DesignerPort { get; init; } = 5002;
}
```

### Launch Settings

```json
{
  "profiles": {
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "DOTNET_ENVIRONMENT": "Development",
        "DOTNET_DASHBOARD_OTLP_ENDPOINT_URL": "https://localhost:21147",
        "DOTNET_RESOURCE_SERVICE_ENDPOINT_URL": "https://localhost:22239"
      }
    }
  }
}
```



## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: AddServiceDefaults Configures OpenTelemetry

*For any* IHostApplicationBuilder, when AddServiceDefaults is called, the resulting service collection SHALL contain registrations for OpenTelemetry logging, tracing, and metrics instrumentation.

**Validates: Requirements 2.2, 2.3, 2.4**

### Property 2: AddServiceDefaults Registers Health Checks

*For any* IHostApplicationBuilder, when AddServiceDefaults is called, the resulting service collection SHALL contain health check service registrations.

**Validates: Requirements 2.5**

### Property 3: Health Endpoints Are Accessible

*For any* running FlowForge.Api instance with ServiceDefaults configured, HTTP GET requests to "/health" and "/alive" SHALL return successful (2xx) responses.

**Validates: Requirements 3.4, 3.5**

### Property 4: Designer Uses Discovered API URL

*For any* Designer configuration where service discovery provides an API URL, the HttpClient base address SHALL be set to the discovered URL.

**Validates: Requirements 4.2**

### Property 5: Designer Falls Back to AppSettings

*For any* Designer configuration where service discovery does NOT provide an API URL, the HttpClient base address SHALL be set to the value from appsettings configuration.

**Validates: Requirements 4.3**

## Error Handling

### Service Startup Failures

- **API fails to start**: AppHost logs the error and marks the resource as unhealthy in the dashboard. Designer will fail to connect but should display appropriate error messaging.
- **Designer fails to start**: AppHost logs the error. API continues to function independently.
- **Database connection failure**: API logs connection errors and health checks report unhealthy status.

### Service Discovery Failures

- **API URL not discoverable**: Designer falls back to appsettings configuration value.
- **Invalid URL format**: Designer logs warning and uses default localhost URL.

### Health Check Failures

- **Health endpoint timeout**: Returns 503 Service Unavailable after configured timeout.
- **Dependency unhealthy**: Health check reports degraded status with details.

### Configuration Errors

- **Missing required configuration**: Application fails fast with descriptive error message.
- **Invalid environment variable**: Logs warning and uses default value where possible.

## Testing Strategy

### Unit Tests

Unit tests verify individual components in isolation:

1. **ServiceDefaults Extension Methods**
   - Verify AddServiceDefaults registers expected services
   - Verify MapDefaultEndpoints maps correct routes
   - Verify ConfigureOpenTelemetry adds telemetry providers

2. **Configuration Parsing**
   - Verify API URL resolution from various configuration sources
   - Verify fallback behavior when configuration is missing

### Property-Based Tests

Property-based tests verify universal properties across many inputs using CsCheck:

1. **Property 1 & 2**: Generate various IHostApplicationBuilder configurations and verify OpenTelemetry and health checks are always registered.

2. **Property 4 & 5**: Generate various configuration combinations and verify correct URL resolution behavior.

### Integration Tests

Integration tests verify end-to-end behavior:

1. **Health Endpoint Tests**
   - Start API with ServiceDefaults
   - Verify /health returns 200 OK
   - Verify /alive returns 200 OK

2. **Service Discovery Tests**
   - Configure Designer with service discovery environment variables
   - Verify HttpClient uses discovered URL

### Test Configuration

```csharp
// Property test configuration using CsCheck
[Fact]
public void AddServiceDefaults_AlwaysRegistersOpenTelemetry()
{
    Gen.Int.Sample(config =>
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults();
        
        var services = builder.Services;
        
        // Verify OpenTelemetry registrations exist
        Assert.Contains(services, s => 
            s.ServiceType.FullName?.Contains("OpenTelemetry") == true);
    }, iter: 100);
}
```

### Test Framework

- **xUnit**: Primary test framework
- **CsCheck**: Property-based testing
- **Microsoft.AspNetCore.Mvc.Testing**: Integration testing for API endpoints
- **NSubstitute**: Mocking external dependencies (if needed)
