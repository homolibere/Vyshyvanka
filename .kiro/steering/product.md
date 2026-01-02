---
inclusion: always
---

# FlowForge Product Overview

FlowForge is a workflow automation platform built on .NET 10. Users create automated workflows through a visual node-based designer.

## Domain Entities

| Entity | Description | Key Rules |
|--------|-------------|-----------|
| Workflow | Process definition with nodes and connections | Must have exactly one trigger node; owned by a user |
| Node | Single operation unit with input/output ports | Belongs to one workflow; type determines base class |
| Connection | Links output port to input port | Must respect port type compatibility |
| Execution | Runtime instance of a workflow | Immutable once status is Completed/Failed/Cancelled |
| Credential | Encrypted authentication data | Never expose in API responses or logs |

## Node Types

| Category | Base Class | Has Input Ports | Examples |
|----------|------------|-----------------|----------|
| Trigger | `BaseTriggerNode` | No | WebhookTrigger, ScheduleTrigger, ManualTrigger |
| Action | `BaseActionNode` | Yes | HttpRequest, DatabaseQuery, EmailSend |
| Logic | `BaseLogicNode` | Yes | If, Switch, Loop, Merge |

When implementing nodes:
- Inherit from the appropriate base class in `FlowForge.Engine/Nodes/Base/`
- Override `ExecuteAsync(IExecutionContext context, CancellationToken ct)`
- Register in `NodeRegistry` for discovery

## Port Types & Compatibility

| PortType | Compatible With |
|----------|-----------------|
| `Any` | All types |
| `Object` | Object, Any |
| `Array` | Array, Any |
| `String` | String, Any |
| `Number` | Number, Any |
| `Boolean` | Boolean, Any |

Connections are valid when: source port type equals target port type, OR either port is `Any`.

## Execution Lifecycle

| Status | Description | Transitions To |
|--------|-------------|----------------|
| `Pending` | Queued, not started | Running, Cancelled |
| `Running` | Currently executing | Completed, Failed, Cancelled |
| `Completed` | Finished successfully | (terminal) |
| `Failed` | Error occurred | (terminal) |
| `Cancelled` | Stopped by user/system | (terminal) |

Execution flow:
1. Trigger fires → Execution created with `Pending` status
2. Engine resolves node order via topological sort
3. Nodes execute sequentially, status becomes `Running`
4. Each node output stored for expression evaluation
5. Final status set to `Completed`, `Failed`, or `Cancelled`

## Expression Syntax

Expressions use double-brace syntax to reference data from previous nodes:

| Pattern | Description |
|---------|-------------|
| `{{$node.NodeName.data}}` | Access output data from named node |
| `{{$node.NodeName.data.propertyName}}` | Access nested property |
| `{{$node.NodeName.data[0]}}` | Access array element |
| `{{$execution.id}}` | Current execution ID |
| `{{$workflow.id}}` | Current workflow ID |

## Workflow Validation Rules

Before execution, validate:
- [ ] Exactly one trigger node exists
- [ ] Trigger node has no incoming connections
- [ ] All non-trigger nodes are reachable from trigger
- [ ] No circular dependencies in connections
- [ ] All required node properties are configured
- [ ] Connection port types are compatible

## User Roles

| Role | Create/Edit | Execute | View | Manage Users |
|------|-------------|---------|------|--------------|
| `Viewer` | ❌ | ❌ | ✅ | ❌ |
| `Editor` | ✅ | ✅ | ✅ | ❌ |
| `Admin` | ✅ | ✅ | ✅ | ✅ |

## Security Rules

**Always:**
- Encrypt credentials at rest using AES-256
- Validate user owns workflow before any operation
- Sanitize user input in expressions
- Use parameterized queries for database operations

**Never:**
- Return credential values in API responses
- Log credential values or sensitive data
- Allow cross-user workflow access without explicit sharing
- Execute unvalidated workflows

## Required Terminology

Use these terms exactly in code, APIs, UI, and documentation:

| Correct | Incorrect |
|---------|-----------|
| Workflow | flow, automation, process |
| Node | step, block, task, action |
| Execution | run, instance, job |
| Credential | secret, auth, key, token |
| Trigger | starter, initiator, entry |
| Connection | edge, link, wire |
