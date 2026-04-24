# FlowForge Documentation

Comprehensive business requirements and architectural documentation for the FlowForge workflow automation platform.

## Table of Contents

| # | Document | Description |
|---|----------|------------|
| 01 | [Product Overview](01-overview.md) | Vision, capabilities, glossary, stakeholders, system context |
| 02 | [Architecture](02-architecture.md) | Tech stack, project structure, dependency rules, deployment, layered architecture, key decisions |
| 03 | [Domain Model](03-domain-model.md) | Entity relationships, enumerations, validation rules, concurrency control, custom exceptions |
| 04 | [Workflow Engine](04-workflow-engine.md) | Execution lifecycle, topological sort, parallel execution, data flow, expression language, cancellation, persistence |
| 05 | [Node System](05-node-system.md) | Node hierarchy, built-in nodes, registry, ports, configuration schema, metadata attributes |
| 06 | [REST API Reference](06-api-reference.md) | All endpoints, authentication, error format, request/response flow |
| 07 | [Security](07-security.md) | JWT and API key auth, RBAC policies, credential encryption, audit logging, error handling |
| 08 | [Plugin System](08-plugin-system.md) | Plugin architecture, NuGet package management, dependency resolution, isolation, creating plugins |
| 09 | [Designer](09-designer.md) | Blazor WASM UI, component structure, services, canvas interaction, client models |
| 10 | [Observability and Testing](10-observability-and-testing.md) | OpenTelemetry, health checks, test strategy, property-based testing, integration tests |
