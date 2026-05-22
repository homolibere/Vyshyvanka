---
inclusion: always
---

# Vyshyvanka Project Structure

```
Vyshyvanka/
├── src/
│   ├── Vyshyvanka.Core/              # Domain layer — zero dependencies on other projects
│   ├── Vyshyvanka.Engine/            # Execution engine, persistence, plugins, auth
│   ├── Vyshyvanka.Api/               # ASP.NET Core REST API
│   ├── Vyshyvanka.AppHost/           # .NET Aspire orchestration (dev hosting)
│   ├── Vyshyvanka.Designer/          # Blazor WASM UI — communicates only via HTTP
│   └── Vyshyvanka.ServiceDefaults/   # Shared service configuration (Aspire defaults)
├── plugins/
│   ├── Vyshyvanka.Plugin.AdvancedHttp/ # HTTP retry, polling, batch, GraphQL nodes
│   ├── Vyshyvanka.Plugin.GitLab/      # GitLab integration nodes
│   ├── Vyshyvanka.Plugin.Jira/        # Jira integration nodes
│   └── Vyshyvanka.Plugin.Tmplt/       # Starter template for new plugins
├── tests/
│   └── Vyshyvanka.Tests/             # All tests (unit, property, integration, E2E)
├── docs/                            # Design documentation and architectural decisions
├── Directory.Packages.props         # Central package version management
└── Vyshyvanka.slnx                   # Solution file
```

## Dependency Rules

Dependencies flow strictly downward. NEVER introduce an upward or circular reference.

| Project | Allowed References | Forbidden References |
|---------|-------------------|----------------------|
| Core | None | Everything else |
| Engine | Core | Api, Designer, AppHost |
| Api | Core, Engine | Designer, AppHost |
| AppHost | Api, ServiceDefaults | Core, Engine directly |
| Designer | None (HTTP calls only) | Core, Engine, Api |
| Plugin.* | Core | Engine, Api, Designer |
| Tests | All projects | — |

## Code Placement

Place new files according to these tables. Namespace MUST match the folder path (e.g., `Vyshyvanka.Engine.Nodes.Actions`).

### Vyshyvanka.Core/
| What | Where |
|------|-------|
| Domain models | `Models/` |
| Interfaces / contracts | `Interfaces/` |
| Enums | `Enums/` |
| Custom exceptions | `Exceptions/` |
| Node / plugin attributes | `Attributes/` |

### Vyshyvanka.Engine/
| What | Where |
|------|-------|
| Trigger nodes | `Nodes/Triggers/` |
| Action nodes | `Nodes/Actions/` |
| Logic nodes | `Nodes/Logic/` |
| Node base classes | `Nodes/Base/` |
| Workflow execution | `Execution/` |
| Expression evaluation | `Expressions/` |
| EF Core DbContext | `Persistence/VyshyvankaDbContext.cs` |
| Repository implementations | `Persistence/` |
| Entity classes | `Persistence/Entities/` |
| EF Core migrations | `Persistence/Migrations/` |
| Plugin system | `Plugins/` |
| Package management | `Packages/` |
| Node registry | `Registry/` |
| Workflow validation | `Validation/` |
| Auth services | `Auth/` |
| LDAP authentication | `Auth/LdapAuthenticationService.cs`, `Auth/LdapAuthService.cs` |
| OIDC user provisioning | `Auth/OidcUserProvisioningService.cs` |
| Credential handling | `Credentials/` |
| Vault/OpenBao client | `Credentials/VaultClient.cs`, `Credentials/VaultCredentialService.cs` |

### Vyshyvanka.Api/
| What | Where |
|------|-------|
| Controllers | `Controllers/` |
| Request/Response DTOs | `Models/` |
| Middleware | `Middleware/` |
| OIDC claims transformation | `Middleware/OidcClaimsTransformation.cs` |
| Authorization policies | `Authorization/` |
| Service extensions | `Extensions/` |
| API-layer services | `Services/` |
| Installed plugin packages | `packages/` |

### Vyshyvanka.Designer/
| What | Where |
|------|-------|
| Canvas & node rendering | `Components/Canvas/` |
| Node editor modal & panels | `Components/NodeEditor/` |
| Typed property editors | `Components/PropertyEditors/` |
| Plugin/package management | `Components/Packages/` |
| Credentials, API keys, sources | `Components/Settings/` |
| Reusable UI (dialogs, toasts) | `Components/Shared/` |
| Workflow browser | `Components/Workflow/` |
| Layout components | `Layout/` |
| Pages | `Pages/` |
| Client services | `Services/` |
| Client models | `Models/` |
| Static assets | `wwwroot/` |
| JavaScript interop | `wwwroot/js/` |

Blazor component rules:
- Each component = up to 3 files: `Name.razor`, `Name.razor.cs`, `Name.razor.css`
- Code-behind class MUST be `partial` and match the component name
- Use `[Inject]` attribute in code-behind — NEVER `@inject` in markup
- No `@code` blocks in `.razor` files
- No `<style>` blocks in `.razor` files
- All components use `@namespace Vyshyvanka.Designer.Components` regardless of subfolder

### Vyshyvanka.Tests/
| What | Where |
|------|-------|
| Unit tests | `Unit/` |
| Property-based tests | `Property/` |
| Integration tests | `Integration/` |
| E2E tests | `E2E/` |
| Test fixtures | `Integration/Fixtures/` |

## Naming Conventions

- File names match the class name: `WorkflowEngine.cs`
- Interfaces prefixed with `I`: `IWorkflowEngine`
- Async methods suffixed with `Async`: `ExecuteAsync`
- Test classes suffixed with `Tests`: `NodeRegistryTests`
- Test methods use `When{Condition}Then{ExpectedResult}`: `WhenWorkflowHasNoTriggerThenValidationFails`

## Service Registration

- Engine services → `Vyshyvanka.Api/Extensions/ServiceCollectionExtensions.cs`
- API services → `Vyshyvanka.Api/Program.cs`
- Designer services → `Vyshyvanka.Designer/Program.cs`

## Ripple-Effect Checklist

When modifying code, ALWAYS check for downstream impact:

| If you change… | Also update… |
|----------------|--------------|
| Interface in Core | All implementations in Engine |
| Repository interface | Corresponding class in `Engine/Persistence/` |
| Node base class | Every node that inherits from it |
| API DTO | Matching model in `Designer/Models/` |
| Controller endpoint | Integration tests in `Tests/Integration/` |
| DbContext or Entity | Add a new EF migration |
| Node implementation | Registration in `NodeRegistry` |
| Authentication provider setting | `ServiceCollectionExtensions`, `AuthController`, `DevelopmentUserSeeder` |
| Credential storage provider setting | `ServiceCollectionExtensions`, `CredentialService` or `VaultCredentialService` |
| `ICredentialService` interface | Both `CredentialService` and `VaultCredentialService` |

## Designer Service Architecture

The Designer's workflow state is decomposed into focused services:

| Service | Responsibility |
|---------|---------------|
| `WorkflowStore` | Shared state container (workflow, node definitions, dirty flag, serialization) |
| `WorkflowEditService` | Workflow mutations (add/remove/move nodes, connections, metadata) |
| `WorkflowValidationService` | Validation logic and port compatibility checks |
| `CanvasStateService` | Pan, zoom, selection, undo/redo, pending connections, drag state |
| `ExecutionStateService` | Execution visualization and node execution states |

Dependency flow: `WorkflowEditService` → `WorkflowStore`, `CanvasStateService`, `WorkflowValidationService`, `ExecutionStateService`. No circular dependencies.

Components inject only the specific services they need. There is no facade or god-object.

## Key Patterns

- Records for DTOs and immutable domain objects
- Repository pattern: interfaces in Core, implementations in Engine
- Node inheritance: `BaseTriggerNode`, `BaseActionNode`, `BaseLogicNode` in `Engine/Nodes/Base/`
- Dependency injection throughout all projects
- Store + decomposed services pattern in Designer (see above)
