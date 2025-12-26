---
inclusion: always
---

# FlowForge Project Structure

```
FlowForge/
├── FlowForge.Core/           # Domain layer - models, interfaces, enums (no dependencies)
├── FlowForge.Engine/         # Execution engine - nodes, plugins, expressions
├── FlowForge.Api/            # REST API - controllers, middleware, DTOs
├── FlowForge.Designer/       # Blazor WASM UI - components, services, pages
├── FlowForge.Plugin.*/       # Plugin projects - custom node implementations
└── FlowForge.Tests/          # All tests - unit, property, integration
```

## Project Dependencies

Dependency direction flows downward. Never create circular references.

| Project | Can Reference |
|---------|---------------|
| Core | None (domain layer) |
| Engine | Core |
| Api | Core, Engine |
| Designer | None (communicates via HTTP) |
| Plugin.* | Core only |
| Tests | All projects |

## Code Placement

| New Code Type | Location |
|---------------|----------|
| Domain models | `Core/Models/` |
| Interfaces | `Core/Interfaces/` |
| Enums | `Core/Enums/` |
| Trigger nodes | `Engine/Nodes/Triggers/` |
| Action nodes | `Engine/Nodes/Actions/` |
| Logic nodes | `Engine/Nodes/Logic/` |
| API endpoints | `Api/Controllers/` |
| API DTOs | `Api/Models/` |
| UI components | `Designer/Components/` |
| UI pages | `Designer/Pages/` |
| Unit tests | `Tests/Unit/` |
| Property tests | `Tests/Property/` |

## Naming Conventions

- **Files**: Match primary class name (`WorkflowEngine.cs` contains `WorkflowEngine`)
- **Namespaces**: Match folder path (`FlowForge.Engine.Nodes.Actions`)
- **Interfaces**: Prefix with `I` (`IWorkflowEngine`, `INode`)
- **Async methods**: Suffix with `Async` (`ExecuteAsync`, `SaveWorkflowAsync`)
- **Test classes**: Mirror source class (`NodeRegistry` → `NodeRegistryTests`)
- **Test methods**: Describe behavior (`WhenWorkflowHasNoTriggerThenValidationFails`)

## Architecture Layers

1. **Presentation**: Designer (Blazor), Api (REST controllers, webhooks)
2. **Application**: Services in Api project orchestrating domain operations
3. **Domain**: Engine (WorkflowEngine, NodeRegistry, ExpressionEvaluator, Plugins)
4. **Infrastructure**: Persistence in Engine (EF Core repositories, DbContext)

## Key Patterns

- **Records** for immutable DTOs and domain objects
- **Repository pattern** for data access (`IWorkflowRepository`, `IExecutionRepository`)
- **Base classes** for node types (`BaseNode`, `BaseTriggerNode`, `BaseActionNode`)
- **Dependency injection** throughout - register in `Program.cs` or extension methods
