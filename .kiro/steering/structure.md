---
inclusion: always
---

# FlowForge Project Structure

```
FlowForge/
├── src/
│   ├── FlowForge.Core/              # Domain layer — zero dependencies on other projects
│   ├── FlowForge.Engine/            # Execution engine, persistence, plugins, auth
│   ├── FlowForge.Api/               # ASP.NET Core REST API
│   ├── FlowForge.AppHost/           # .NET Aspire orchestration (dev hosting)
│   ├── FlowForge.Designer/          # Blazor WASM UI — communicates only via HTTP
│   └── FlowForge.ServiceDefaults/   # Shared service configuration (Aspire defaults)
├── plugins/
│   └── FlowForge.Plugin.*/          # Plugin projects (reference Core only)
├── tests/
│   └── FlowForge.Tests/             # All tests (unit, property, integration, E2E)
├── docs/                            # Design documentation and architectural decisions
├── Directory.Packages.props         # Central package version management
└── FlowForge.slnx                   # Solution file
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

Place new files according to these tables. Namespace MUST match the folder path (e.g., `FlowForge.Engine.Nodes.Actions`).

### FlowForge.Core/
| What | Where |
|------|-------|
| Domain models | `Models/` |
| Interfaces / contracts | `Interfaces/` |
| Enums | `Enums/` |
| Custom exceptions | `Exceptions/` |

### FlowForge.Engine/
| What | Where |
|------|-------|
| Trigger nodes | `Nodes/Triggers/` |
| Action nodes | `Nodes/Actions/` |
| Logic nodes | `Nodes/Logic/` |
| Node base classes | `Nodes/Base/` |
| Workflow execution | `Execution/` |
| Expression evaluation | `Expressions/` |
| EF Core DbContext | `Persistence/FlowForgeDbContext.cs` |
| Repository implementations | `Persistence/` |
| Entity classes | `Persistence/Entities/` |
| Plugin system | `Plugins/` |
| Package management | `Packages/` |
| Node registry | `Registry/` |
| Workflow validation | `Validation/` |
| Auth services | `Auth/` |
| Credential handling | `Credentials/` |

### FlowForge.Api/
| What | Where |
|------|-------|
| Controllers | `Controllers/` |
| Request/Response DTOs | `Models/` |
| Middleware | `Middleware/` |
| Authorization policies | `Authorization/` |
| Service extensions | `Extensions/` |
| API-layer services | `Services/` |

### FlowForge.Designer/
| What | Where |
|------|-------|
| Blazor components | `Components/` |
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

### FlowForge.Tests/
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

- Engine services → `FlowForge.Api/Extensions/ServiceCollectionExtensions.cs`
- API services → `FlowForge.Api/Program.cs`
- Designer services → `FlowForge.Designer/Program.cs`

## Ripple-Effect Checklist

When modifying code, ALWAYS check for downstream impact:

| If you change… | Also update… |
|----------------|--------------|
| Interface in Core | All implementations in Engine |
| Repository interface | Corresponding class in `Engine/Persistence/` |
| Node base class | Every node that inherits from it |
| API DTO | Matching model in `Designer/Models/` |
| Controller endpoint | Integration tests in `Tests/Integration/` |
| DbContext or Entity | EF migrations may be needed |
| Node implementation | Registration in `NodeRegistry` |

## Key Patterns

- Records for DTOs and immutable domain objects
- Repository pattern: interfaces in Core, implementations in Engine
- Node inheritance: `BaseTriggerNode`, `BaseActionNode`, `BaseLogicNode` in `Engine/Nodes/Base/`
- Dependency injection throughout all projects
