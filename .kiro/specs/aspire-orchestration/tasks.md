# Implementation Plan: Aspire Orchestration

## Overview

This implementation plan adds .NET Aspire orchestration to FlowForge by creating two new projects (FlowForge.AppHost and FlowForge.ServiceDefaults) and integrating them with the existing Api and Designer services. Tasks are ordered to build incrementally, with each step validating core functionality.

## Tasks

- [x] 1. Create FlowForge.ServiceDefaults project
  - [x] 1.1 Create the FlowForge.ServiceDefaults project with .NET 10 target
    - Create project file with Aspire SDK references
    - Add required NuGet packages for OpenTelemetry and service discovery
    - _Requirements: 2.6, 7.4_

  - [x] 1.2 Implement AddServiceDefaults extension method
    - Create Extensions.cs with AddServiceDefaults method
    - Configure OpenTelemetry for logging, tracing, and metrics
    - Register health checks
    - Configure service discovery and HTTP client defaults
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 1.3 Implement MapDefaultEndpoints extension method
    - Add health endpoint at "/health"
    - Add liveness endpoint at "/alive"
    - _Requirements: 3.4, 3.5_

  - [x] 1.4 Write property test for AddServiceDefaults OpenTelemetry configuration
    - **Property 1: AddServiceDefaults Configures OpenTelemetry**
    - **Validates: Requirements 2.2, 2.3, 2.4**

  - [x] 1.5 Write property test for AddServiceDefaults health check registration
    - **Property 2: AddServiceDefaults Registers Health Checks**
    - **Validates: Requirements 2.5**

- [x] 2. Create FlowForge.AppHost project
  - [x] 2.1 Create the FlowForge.AppHost project with Aspire.AppHost.Sdk
    - Create project file with IsAspireHost property
    - Add project references to FlowForge.Api and FlowForge.Designer
    - _Requirements: 1.6, 7.3_

  - [x] 2.2 Implement AppHost Program.cs
    - Create DistributedApplication builder
    - Add FlowForge.Api as project resource named "api"
    - Add FlowForge.Designer as project resource named "designer"
    - Configure Designer to reference Api for service discovery
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [x] 2.3 Add launch settings for development
    - Create launchSettings.json with https profile
    - Configure environment variables for Aspire dashboard
    - _Requirements: 8.4_

- [x] 3. Integrate FlowForge.Api with ServiceDefaults
  - [x] 3.1 Add ServiceDefaults reference to FlowForge.Api
    - Add project reference to FlowForge.ServiceDefaults
    - _Requirements: 3.1_

  - [x] 3.2 Update FlowForge.Api Program.cs
    - Call AddServiceDefaults on builder
    - Call MapDefaultEndpoints on app
    - _Requirements: 3.2, 3.3_

  - [x] 3.3 Write integration test for health endpoints
    - **Property 3: Health Endpoints Are Accessible**
    - **Validates: Requirements 3.4, 3.5**

- [x] 4. Integrate FlowForge.Designer with service discovery
  - [x] 4.1 Update FlowForge.Designer Program.cs for service discovery
    - Read API URL from service discovery configuration
    - Implement fallback to appsettings value
    - Configure HttpClient with resolved URL
    - _Requirements: 4.1, 4.2, 4.3_

  - [x] 4.2 Write property test for API URL resolution
    - **Property 4: Designer Uses Discovered API URL**
    - **Validates: Requirements 4.2**

  - [x] 4.3 Write property test for fallback behavior
    - **Property 5: Designer Falls Back to AppSettings**
    - **Validates: Requirements 4.3**

- [x] 5. Update solution structure
  - [x] 5.1 Add new projects to FlowForge.sln
    - Add FlowForge.AppHost to solution
    - Add FlowForge.ServiceDefaults to solution
    - _Requirements: 7.1, 7.2_

  - [x] 5.2 Update Directory.Packages.props with Aspire packages
    - Add Aspire SDK version
    - Add OpenTelemetry package versions
    - Add service discovery package versions

- [x] 6. Add optional PostgreSQL support
  - [x] 6.1 Add PostgreSQL hosting package to AppHost
    - Add Aspire.Hosting.PostgreSQL package reference
    - _Requirements: 5.1_

  - [x] 6.2 Implement conditional PostgreSQL configuration
    - Add environment variable check for PostgreSQL mode
    - Configure PostgreSQL resource when enabled
    - Inject connection string into Api
    - Maintain SQLite as default for development
    - _Requirements: 5.2, 5.3, 5.4, 8.1, 8.2, 8.3_

- [x] 7. Checkpoint - Verify full integration
  - Ensure all tests pass
  - Verify AppHost starts all services correctly
  - Verify Aspire Dashboard displays resources
  - Ask the user if questions arise

## Notes

- All tasks including property tests are required for comprehensive coverage
- Each task references specific requirements for traceability
- The ServiceDefaults project must be created first as other projects depend on it
- Property tests use CsCheck for property-based testing
- Integration tests use Microsoft.AspNetCore.Mvc.Testing
