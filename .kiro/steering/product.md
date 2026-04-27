---
inclusion: always
---

# FlowForge Product Domain

FlowForge is a .NET 10 workflow automation platform. Users build workflows visually by connecting nodes in a Blazor WebAssembly designer, then execute them via a REST API backed by a workflow engine.

## Domain Model

| Entity | Description | Invariants |
|--------|-------------|------------|
| Workflow | Directed graph of nodes and connections | Exactly one trigger node; owned by a single user |
| Node | Atomic operation with typed input/output ports | Belongs to one workflow; category determines base class |
| Connection | Directed link from an output port to an input port | Port types must be compatible (see below) |
| Execution | Immutable runtime record of a workflow run | Terminal states (`Completed`, `Failed`, `Cancelled`) are final — never mutate |
| Credential | Authentication data stored locally (AES-256) or in an external secrets manager (Vault, OpenBao) | Never returned in API responses; never logged |

## Node Categories

| Category | Base Class | Input Ports | Examples |
|----------|------------|-------------|----------|
| Trigger | `BaseTriggerNode` | None | WebhookTrigger, ScheduleTrigger, ManualTrigger |
| Action | `BaseActionNode` | Yes | HttpRequest, DatabaseQuery, EmailSend |
| Logic | `BaseLogicNode` | Yes | If, Switch, Loop, Merge |

When implementing a new node:
1. Inherit from the correct base class in `FlowForge.Engine/Nodes/Base/`
2. Override `ExecuteAsync(IExecutionContext context, CancellationToken ct)`
3. Register the node in `NodeRegistry` so the engine can discover it

## Port Type Compatibility

A connection is valid when the source port type equals the target port type, OR either port is `Any`.

| PortType | Compatible With |
|----------|-----------------|
| `Any` | All types |
| `Object` | Object, Any |
| `Array` | Array, Any |
| `String` | String, Any |
| `Number` | Number, Any |
| `Boolean` | Boolean, Any |

## Execution State Machine

```
Pending ──→ Running ──→ Completed
  │            │
  │            ├──→ Failed
  │            │
  └──→ Cancelled ←──┘
```

`Completed`, `Failed`, and `Cancelled` are terminal — no further transitions allowed.

Execution sequence:
1. Trigger fires → new Execution in `Pending`
2. Engine resolves node order via topological sort → status becomes `Running`
3. Each node executes; its output is stored for downstream expression evaluation
4. Final status set based on outcome

## Expression Syntax

Double-brace expressions reference data from earlier nodes or execution metadata:

| Pattern | Resolves To |
|---------|-------------|
| `{{$node.NodeName.data}}` | Full output of the named node |
| `{{$node.NodeName.data.prop}}` | Nested property access |
| `{{$node.NodeName.data[0]}}` | Array index access |
| `{{$execution.id}}` | Current execution ID |
| `{{$workflow.id}}` | Current workflow ID |

Always sanitize user-supplied values before embedding them in expressions.

## Workflow Validation (pre-execution)

All of these must pass before a workflow can execute:
- Exactly one trigger node exists
- Trigger node has no incoming connections
- All non-trigger nodes are reachable from the trigger
- No circular dependencies exist in the connection graph
- All required node configuration properties are set
- Every connection satisfies port type compatibility

## User Roles & Permissions

| Role | Create/Edit | Execute | View | Manage Users |
|------|:-----------:|:-------:|:----:|:------------:|
| Viewer | ✗ | ✗ | ✓ | ✗ |
| Editor | ✓ | ✓ | ✓ | ✗ |
| Admin | ✓ | ✓ | ✓ | ✓ |

Always check `ICurrentUserService` to verify the authenticated user owns or has access to a resource before performing any operation.

## Security Rules

**Always do:**
- Encrypt credentials at rest with AES-256 (built-in) or delegate to Vault/OpenBao
- Validate resource ownership before any CRUD or execution operation
- Sanitize user input in expressions to prevent injection
- Use parameterized queries for all database operations

**Never do:**
- Return credential values in any API response
- Log credentials or sensitive data at any log level
- Allow cross-user workflow access without explicit sharing
- Execute a workflow that has not passed validation
- Store Vault/OpenBao tokens in plain text in appsettings.json (use environment variables)

## Required Terminology

Use these terms consistently in code, APIs, UI text, comments, and documentation. Do not substitute synonyms.

| Correct Term | Do Not Use |
|--------------|------------|
| Workflow | flow, automation, process |
| Node | step, block, task, action |
| Execution | run, instance, job |
| Credential | secret, auth, key, token |
| Trigger | starter, initiator, entry |
| Connection | edge, link, wire |
