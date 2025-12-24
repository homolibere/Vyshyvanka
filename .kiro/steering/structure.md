# FlowForge Project Structure

```
FlowForge/
├── FlowForge.Core/           # Domain layer
│   ├── Models/               # Data models (Workflow, Node, Execution, etc.)
│   ├── Interfaces/           # Core interfaces (IWorkflowEngine, INode, etc.)
│   └── Enums/                # NodeCategory, ExecutionStatus, etc.
│
├── FlowForge.Engine/         # Workflow execution
│   ├── Execution/            # WorkflowEngine, ExecutionContext
│   ├── Nodes/                # Built-in node implementations
│   │   ├── Triggers/         # ManualTrigger, Webhook, Schedule
│   │   ├── Actions/          # HttpRequest, Database, Email, File
│   │   └── Logic/            # If, Switch, Merge, Loop
│   ├── Expressions/          # Expression evaluator
│   ├── Registry/             # NodeRegistry implementation
│   └── Plugins/              # Plugin loader and host
│
├── FlowForge.Api/            # REST API
│   ├── Controllers/          # Workflow, Execution, Credential controllers
│   ├── Middleware/           # Auth, error handling
│   ├── Services/             # Application services
│   └── Webhooks/             # Webhook trigger endpoints
│
├── FlowForge.Designer/       # Blazor WebAssembly
│   ├── Components/           # Canvas, NodePalette, ConfigPanel
│   ├── Services/             # State management, API client
│   └── wwwroot/              # Static assets
│
└── FlowForge.Tests/          # All tests
    ├── Unit/                 # Unit tests per component
    ├── Property/             # CsCheck property-based tests
    └── Integration/          # End-to-end tests
```

## Architecture Layers

1. **Presentation**: Blazor Designer, REST API, Webhooks
2. **Application**: WorkflowService, ExecutionService, CredentialService, UserService
3. **Domain**: WorkflowEngine, NodeRegistry, ExpressionEvaluator, PluginSystem
4. **Infrastructure**: Database (EF Core), Cache (Redis), Message Queue

## Conventions

- Records for immutable data models (DTOs, domain objects)
- Interfaces prefixed with `I` (IWorkflowEngine, INode)
- Async methods suffixed with `Async`
- Test classes mirror source: `CatDoor` → `CatDoorTests`
- Test names describe behavior: `WhenCatMeowsThenCatDoorOpens`
