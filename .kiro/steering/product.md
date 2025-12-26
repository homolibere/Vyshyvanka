---
inclusion: always
---

# FlowForge Product Overview

FlowForge is a workflow automation platform inspired by n8n, built on .NET 10. Users create automated workflows through a visual node-based designer.

## Domain Model

| Entity | Description | Relationships |
|--------|-------------|---------------|
| Workflow | Automated process definition | Contains nodes and connections |
| Node | Single operation unit | Belongs to workflow, has input/output ports |
| Connection | Data flow link | Connects output port to input port |
| Execution | Runtime instance of workflow | Tracks status, logs, node outputs |
| Credential | Encrypted auth data | Referenced by nodes needing external access |

## Node Categories

| Type | Purpose | Examples |
|------|---------|----------|
| Trigger | Starts workflow execution | Webhook, Schedule, Manual |
| Action | Performs operations | HTTP Request, Database Query, Email |
| Logic | Controls flow | If/Else, Switch, Loop, Merge |

**Rules**:
- Every workflow must have exactly one trigger node
- Trigger nodes have no input ports
- Data flows from trigger through actions/logic nodes
- Connections respect port type compatibility

## Execution Semantics

1. Trigger fires (webhook received, schedule hit, manual start)
2. Engine resolves execution order via topological sort
3. Nodes execute sequentially, passing data through connections
4. Expressions evaluate using previous node outputs (`{{$node.NodeName.data}}`)
5. Execution completes with success/failure status

## User Roles

| Role | Permissions |
|------|-------------|
| Admin | Full access, user management, system config |
| Editor | Create, edit, execute workflows |
| Viewer | Read-only access to workflows and executions |

## Business Constraints

- Credentials are never exposed in API responses or logs
- Workflow validation must pass before execution
- Plugins run in isolated contexts, cannot access other workflows
- Webhook URLs are unique per workflow
- Executions are immutable once completed

## Terminology

Use these terms consistently:
- "Workflow" not "flow" or "automation"
- "Node" not "step" or "block"
- "Execution" not "run" or "instance"
- "Credential" not "secret" or "auth"
- "Trigger" not "starter" or "initiator"
