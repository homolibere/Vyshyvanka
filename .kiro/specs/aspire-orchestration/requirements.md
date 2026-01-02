# Requirements Document

## Introduction

This document defines the requirements for adding .NET Aspire orchestration to FlowForge. Aspire provides a unified development experience for distributed applications by orchestrating multiple services (FlowForge.Api and FlowForge.Designer), managing service discovery, providing observability through a built-in dashboard, and simplifying configuration management across environments.

## Glossary

- **AppHost**: The Aspire orchestration project that defines and coordinates all application services and their dependencies
- **ServiceDefaults**: A shared project containing common configuration for telemetry, health checks, and resilience patterns
- **Aspire_Dashboard**: The built-in web interface for monitoring logs, traces, metrics, and service health
- **Resource**: Any service, container, or infrastructure component managed by the AppHost
- **Service_Discovery**: Automatic resolution of service endpoints without hard-coded URLs
- **OpenTelemetry**: The observability framework used for distributed tracing, metrics, and logging

## Requirements

### Requirement 1: AppHost Orchestration Project

**User Story:** As a developer, I want a single entry point to start all FlowForge services, so that I can run the entire application with one command.

#### Acceptance Criteria

1. THE AppHost SHALL define FlowForge.Api as a project resource with the name "api"
2. THE AppHost SHALL define FlowForge.Designer as a project resource with the name "designer"
3. THE AppHost SHALL configure the Designer to reference the Api for service discovery
4. WHEN the AppHost starts, THE AppHost SHALL launch all configured services in dependency order
5. WHEN the AppHost starts, THE AppHost SHALL open the Aspire Dashboard automatically
6. THE AppHost SHALL be located in a project named "FlowForge.AppHost"

### Requirement 2: Service Defaults Configuration

**User Story:** As a developer, I want shared configuration for telemetry and health checks, so that all services have consistent observability without duplicating code.

#### Acceptance Criteria

1. THE ServiceDefaults project SHALL provide an AddServiceDefaults extension method
2. WHEN AddServiceDefaults is called, THE ServiceDefaults SHALL configure OpenTelemetry for logging
3. WHEN AddServiceDefaults is called, THE ServiceDefaults SHALL configure OpenTelemetry for distributed tracing
4. WHEN AddServiceDefaults is called, THE ServiceDefaults SHALL configure OpenTelemetry for metrics
5. WHEN AddServiceDefaults is called, THE ServiceDefaults SHALL register default health check endpoints
6. THE ServiceDefaults project SHALL be located in a project named "FlowForge.ServiceDefaults"

### Requirement 3: API Service Integration

**User Story:** As a developer, I want FlowForge.Api to integrate with Aspire, so that it benefits from service discovery and observability features.

#### Acceptance Criteria

1. THE FlowForge.Api project SHALL reference the FlowForge.ServiceDefaults project
2. WHEN FlowForge.Api starts, THE FlowForge.Api SHALL call AddServiceDefaults to configure telemetry
3. WHEN FlowForge.Api starts, THE FlowForge.Api SHALL call MapDefaultEndpoints to expose health check endpoints
4. THE FlowForge.Api SHALL expose a health endpoint at "/health"
5. THE FlowForge.Api SHALL expose a liveness endpoint at "/alive"

### Requirement 4: Designer Service Integration

**User Story:** As a developer, I want FlowForge.Designer to discover the API endpoint automatically, so that I don't need to hard-code URLs for different environments.

#### Acceptance Criteria

1. WHEN the Designer is launched via AppHost, THE Designer SHALL receive the Api base URL through service discovery
2. THE Designer SHALL use the discovered Api URL for all HTTP client configurations
3. IF the Api URL is not provided via service discovery, THEN THE Designer SHALL fall back to the configured appsettings value

### Requirement 5: Database Resource Configuration

**User Story:** As a developer, I want database resources managed by Aspire, so that connection strings are automatically injected into services.

#### Acceptance Criteria

1. THE AppHost SHALL support configuring a PostgreSQL database resource for production scenarios
2. WHEN a PostgreSQL resource is configured, THE AppHost SHALL inject the connection string into FlowForge.Api
3. THE AppHost SHALL support running without a database resource for SQLite development scenarios
4. WHEN running with SQLite, THE FlowForge.Api SHALL use the existing SQLite configuration

### Requirement 6: Aspire Dashboard Access

**User Story:** As a developer, I want to monitor all services through the Aspire Dashboard, so that I can observe logs, traces, and metrics in one place.

#### Acceptance Criteria

1. WHEN the AppHost is running, THE Aspire_Dashboard SHALL display all registered resources
2. THE Aspire_Dashboard SHALL show real-time logs from FlowForge.Api
3. THE Aspire_Dashboard SHALL show real-time logs from FlowForge.Designer
4. THE Aspire_Dashboard SHALL display distributed traces across services
5. THE Aspire_Dashboard SHALL show health status for each service

### Requirement 7: Solution Structure

**User Story:** As a developer, I want the Aspire projects properly integrated into the solution, so that the build and development workflow remains consistent.

#### Acceptance Criteria

1. THE FlowForge.sln SHALL include the FlowForge.AppHost project
2. THE FlowForge.sln SHALL include the FlowForge.ServiceDefaults project
3. THE FlowForge.AppHost project SHALL target .NET 10
4. THE FlowForge.ServiceDefaults project SHALL target .NET 10
5. WHEN running "dotnet run --project FlowForge.AppHost", THE system SHALL start all FlowForge services

### Requirement 8: Environment Configuration

**User Story:** As a developer, I want environment-specific configurations, so that I can run different setups for development and production.

#### Acceptance Criteria

1. THE AppHost SHALL support a development configuration using SQLite
2. THE AppHost SHALL support a production configuration using PostgreSQL
3. WHEN environment variables are set, THE AppHost SHALL use them to configure resources
4. THE AppHost SHALL provide launch settings for common development scenarios
