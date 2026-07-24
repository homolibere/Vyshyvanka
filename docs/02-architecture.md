# Architecture

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10, C# 14 |
| API | ASP.NET Core (REST) |
| UI | Blazor WebAssembly |
| ORM | Entity Framework Core (code-first) |
| Database | PostgreSQL (default), SQLite (opt-in for CI) |
| Orchestration | .NET Aspire 9.1 |
| Observability | OpenTelemetry (traces, metrics, logs) |
| Serialization | System.Text.Json |
| Authentication | JWT Bearer + API Key (dual scheme); configurable OIDC (Keycloak, Authentik) or LDAP |
| Encryption | AES-256 for credentials at rest; optional HashiCorp Vault / OpenBao integration |
| Package Management | NuGet Protocol |

## Project Structure

```mermaid
graph TD
    AppHost["Vyshyvanka.AppHost<br/><i>.NET Aspire Host</i>"]
    Designer["Vyshyvanka.Designer<br/><i>Blazor WASM</i>"]
    Api["Vyshyvanka.Api<br/><i>ASP.NET Core</i>"]
    Engine["Vyshyvanka.Engine<br/><i>Execution, Persistence, Plugins</i>"]
    Contracts["Vyshyvanka.Contracts<br/><i>Shared API DTOs</i>"]
    Core["Vyshyvanka.Core<br/><i>Domain Models, Interfaces</i>"]
    ServiceDefaults["Vyshyvanka.ServiceDefaults<br/><i>Aspire Shared Config</i>"]
    Plugin["Vyshyvanka.Plugin.*<br/><i>Extension Packages</i>"]
    Tests["Vyshyvanka.Tests<br/><i>Unit, Property, Integration</i>"]

    AppHost --> Api
    AppHost --> Designer
    Designer --> Contracts
    Designer -->|HTTP| Api
    Api --> Contracts
    Api --> Engine
    Api --> Core
    Api --> ServiceDefaults
    Contracts --> Core
    Engine --> Core
    Plugin --> Core
    Tests --> Api
    Tests --> Engine
    Tests --> Core
    Tests --> Designer
    Tests --> Contracts
    Tests --> ServiceDefaults
```

## Dependency Rules

Dependencies flow strictly downward. Violations cause circular reference build errors.

| Project | Can Reference | Must Not Reference |
|---------|--------------|-------------------|
| Core | Nothing | Any other project |
| Contracts | Core | Engine, Api, Designer |
| Engine | Core | Api, Designer, Contracts |
| Api | Core, Engine, Contracts, ServiceDefaults | Designer |
| Designer | Contracts | Core (directly), Engine, Api |
| Plugin.* | Core | Engine, Api, Designer, Contracts |
| Tests | All projects | — |

The Designer communicates with the API exclusively over HTTP. It shares request/response types via the Contracts library but never references Engine or Api assemblies directly.

## Deployment Topology

```mermaid
graph LR
    subgraph Aspire["Aspire AppHost"]
        direction TB
        API["API Service<br/>ASP.NET Core"]
        DB[(Database<br/>PostgreSQL / SQLite)]
        WASM["Designer<br/>Blazor WASM"]
    end

    User["Browser"] -->|HTTPS| WASM
    WASM -->|HTTP + Service Discovery| API
    API --> DB
    External["External Systems"] -->|Webhook / API Key| API
    API -->|NuGet Protocol| NuGet["NuGet Feeds"]
```

The Aspire AppHost orchestrates both services with three database modes:

1. **Existing PostgreSQL** (if `ConnectionStrings:vyshyvankadb` is set in AppHost config) — connects to your local/external PostgreSQL instance, no container needed.
2. **Aspire-managed container** (default when no connection string is provided) — spins up a PostgreSQL Docker container with a persistent data volume.
3. **SQLite** (`Database:Provider=Sqlite`) — file-based, no container or external database required.

For standalone deployments (without Aspire), configure `Database:Provider` and `ConnectionStrings:vyshyvankadb` in `appsettings.json` or environment variables.

## Layered Architecture

```mermaid
graph TB
    subgraph Presentation
        Designer["Designer<br/>Blazor WASM Pages & Components"]
        Controllers["API Controllers<br/>REST Endpoints"]
    end

    subgraph Application
        Services["API Services<br/>CurrentUserService, Auth"]
        Middleware["Middleware<br/>Error Handling, API Key Auth"]
    end

    subgraph Domain
        EngineExec["WorkflowEngine<br/>Execution, Validation"]
        Registry["NodeRegistry<br/>Node Discovery"]
        Expressions["ExpressionEvaluator<br/>Template Resolution"]
        Plugins["PluginHost<br/>Isolation, Loading"]
    end

    subgraph Infrastructure
        Persistence["Repositories<br/>EF Core + PostgreSQL/SQLite"]
        Auth["Auth Services<br/>JWT, Password Hashing"]
        Credentials["Credential Services<br/>AES-256 Encryption"]
        Packages["Package Manager<br/>NuGet Protocol"]
    end

    subgraph Foundation
        CoreModels["Core Models<br/>Workflow, Node, Execution"]
        CoreInterfaces["Core Interfaces<br/>Contracts"]
        CoreEnums["Core Enums<br/>Status, Roles, Types"]
    end

    Designer --> Controllers
    Controllers --> Services
    Controllers --> EngineExec
    Services --> Middleware
    EngineExec --> Registry
    EngineExec --> Expressions
    EngineExec --> Plugins
    Registry --> Persistence
    EngineExec --> Persistence
    Auth --> Persistence
    Credentials --> Persistence
    Packages --> Plugins
    Persistence --> CoreModels
    Persistence --> CoreInterfaces
```

## Key Architectural Decisions

| Decision | Rationale |
|----------|-----------|
| Records for domain models | Immutability by default; value equality semantics; concise syntax with `with` expressions |
| Repository pattern via interfaces in Core | Decouples domain from persistence; enables in-memory testing |
| Decorator pattern for PersistentWorkflowEngine | Separates execution logic from persistence concerns; inner engine is testable in isolation |
| Plugin isolation via AssemblyLoadContext | Prevents plugin failures from crashing the host; enables hot-unloading |
| Central package version management | `Directory.Packages.props` ensures consistent dependency versions across all projects |
| Dual authentication scheme | JWT for interactive sessions; API keys for programmatic and webhook access |
| Pluggable authentication provider | Configurable via `appsettings.json` — built-in JWT, Keycloak, Authentik OIDC, or LDAP directory with JIT user provisioning |
| Topological sort for execution order | Guarantees correct data flow; detects cycles at execution time |
| Optimistic concurrency on workflows | Version field prevents lost updates in concurrent editing scenarios |
