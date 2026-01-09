---
inclusion: always
---

# FlowForge Project Structure

```
FlowForge/
├── FlowForge.Core/           # Domain layer (no dependencies)
├── FlowForge.Engine/         # Execution engine, persistence, plugins
├── FlowForge.Api/            # REST API
├── FlowForge.Designer/       # Blazor WASM UI
├── FlowForge.Plugin.*/       # Plugin projects
├── FlowForge.Tests/          # All tests
└── FlowForge.ServiceDefaults/# Shared service configuration
```

## Dependency Rules

Dependencies flow downward only. Violations cause circular reference build errors.

| Project | Can Reference | NEVER Reference |
|---------|---------------|-----------------|
| Core | None | Any other project |
| Engine | Core | Api, Designer |
| Api | Core, Engine | Designer |
| Designer | None (HTTP only) | Core, Engine, Api |
| Plugin.* | Core | Engine, Api, Designer |
| Tests | All | - |

## Code Placement

### Core Project (`FlowForge.Core/`)
| Type | Path |
|------|------|
| Domain models | `Models/` |
| Interfaces | `Interfaces/` |
| Enums | `Enums/` |
| Custom exceptions | `Exceptions/` |

### Engine Project (`FlowForge.Engine/`)
| Type | Path |
|------|------|
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

### Api Project (`FlowForge.Api/`)
| Type | Path |
|------|------|
| Controllers | `Controllers/` |
| Request/Response DTOs | `Models/` |
| Middleware | `Middleware/` |
| Authorization policies | `Authorization/` |
| Service extensions | `Extensions/` |
| API services | `Services/` |

### Designer Project (`FlowForge.Designer/`)
| Type | Path |
|------|------|
| Blazor components | `Components/` |
| Pages | `Pages/` |
| Client services | `Services/` |
| Client models | `Models/` |
| Static assets | `wwwroot/` |
| JavaScript interop | `wwwroot/js/` |

### Tests Project (`FlowForge.Tests/`)
| Type | Path |
|------|------|
| Unit tests | `Unit/` |
| Property-based tests | `Property/` |
| Integration tests | `Integration/` |
| E2E tests | `E2E/` |
| Test fixtures | `Integration/Fixtures/` |

## Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Files | Match class name | `WorkflowEngine.cs` |
| Namespaces | Match folder path | `FlowForge.Engine.Nodes.Actions` |
| Interfaces | Prefix `I` | `IWorkflowEngine` |
| Async methods | Suffix `Async` | `ExecuteAsync` |
| Test classes | Suffix `Tests` | `NodeRegistryTests` |
| Test methods | `When...Then...` | `WhenWorkflowHasNoTriggerThenValidationFails` |

## Blazor Component Pattern

Each component consists of up to three files:
```
ComponentName.razor      # Markup
ComponentName.razor.cs   # Code-behind (partial class)
ComponentName.razor.css  # Scoped styles (optional)
```

## Service Registration

- **Engine services**: Register in `Api/Extensions/ServiceCollectionExtensions.cs`
- **API services**: Register in `Api/Program.cs`
- **Designer services**: Register in `Designer/Program.cs`

## When Modifying Code

| If changing... | Also check... |
|----------------|---------------|
| Interface in Core | Implementations in Engine |
| Repository interface | Repository in `Engine/Persistence/` |
| Node base class | All nodes inheriting from it |
| API DTO | Designer models in `Designer/Models/` |
| Controller endpoint | Integration tests in `Tests/Integration/` |
| DbContext/Entity | EF migrations may be needed |
| Node implementation | Register in `NodeRegistry` |

## Architecture Layers

1. **Presentation**: Designer (Blazor WASM), Api (REST controllers)
2. **Application**: Services in Api orchestrating operations
3. **Domain**: Engine (WorkflowEngine, NodeRegistry, ExpressionEvaluator)
4. **Infrastructure**: Engine/Persistence (EF Core, repositories)

## Key Patterns

- **Records** for DTOs and immutable domain objects
- **Repository pattern** via interfaces in Core, implementations in Engine
- **Base classes** for nodes: `BaseNode`, `BaseTriggerNode`, `BaseActionNode`, `BaseLogicNode`
- **Dependency injection** throughout all projects

**Rules**:
- No `@code` blocks in `.razor` files
- No `<style>` blocks in `.razor` files
- Code-behind class must be `partial` and match component name
- Use `[Inject]` attribute in code-behind, not `@inject` in markup