# FlowForge

A workflow automation platform built on .NET 10. Users create automated workflows through a visual node-based designer, connect nodes to define data flow, and execute workflows triggered by webhooks, schedules, or manual actions.

## Architecture

```
┌─────────────────┐     ┌─────────────────┐
│    Designer      │────▶│      API        │
│  (Blazor WASM)   │ HTTP│  (ASP.NET Core) │
└─────────────────┘     └────────┬────────┘
                                 │
                        ┌────────┴────────┐
                        │     Engine      │
                        │  (Execution,    │
                        │   Persistence,  │
                        │   Plugins)      │
                        └────────┬────────┘
                                 │
                        ┌────────┴────────┐
                        │      Core       │
                        │  (Domain Models,│
                        │   Interfaces)   │
                        └─────────────────┘
```

Dependencies flow downward only. The Designer communicates with the API exclusively over HTTP.

## Tech Stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 10 / C# 14 |
| API | ASP.NET Core |
| UI | Blazor WebAssembly |
| Database | SQLite (dev) / PostgreSQL (prod) |
| ORM | Entity Framework Core (code-first) |
| Orchestration | .NET Aspire |
| Auth | JWT Bearer + API Key |
| Serialization | System.Text.Json |
| Testing | xUnit, CsCheck, NSubstitute, bUnit |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling) (`dotnet workload install aspire`)
- Docker (optional, for PostgreSQL via Aspire)

## Getting Started

Clone the repo and restore dependencies:

```bash
git clone <repository-url>
cd FlowForge
dotnet restore FlowForge.slnx
```

### Run with Aspire (recommended)

Starts both the API and Designer with service discovery:

```bash
dotnet run --project src/FlowForge.AppHost
```

This uses SQLite by default. To use PostgreSQL instead (requires Docker):

```bash
USE_POSTGRES=true dotnet run --project src/FlowForge.AppHost
```

### Run individual projects

```bash
# API only
dotnet run --project src/FlowForge.Api

# Designer only
dotnet run --project src/FlowForge.Designer
```

### Build & Test

```bash
dotnet build
dotnet test
```

## Project Structure

| Project | Location | Description |
|---------|----------|-------------|
| `FlowForge.Core` | `src/` | Domain models, interfaces, enums, exceptions. No external dependencies. |
| `FlowForge.Engine` | `src/` | Workflow execution engine, EF Core persistence, plugin system, node registry. |
| `FlowForge.Api` | `src/` | REST API controllers, middleware, authentication, DTOs. |
| `FlowForge.Designer` | `src/` | Blazor WebAssembly visual workflow editor. |
| `FlowForge.AppHost` | `src/` | .NET Aspire host for orchestrating services. |
| `FlowForge.ServiceDefaults` | `src/` | Shared Aspire service configuration (OpenTelemetry, resilience). |
| `FlowForge.Plugin.AdvancedHttp` | `plugins/` | HTTP retry, polling, batch, and GraphQL nodes. |
| `FlowForge.Plugin.GitLab` | `plugins/` | GitLab issues, merge requests, pipelines, files, tags, releases. |
| `FlowForge.Plugin.Jira` | `plugins/` | Jira issues, comments, users, and JQL search. |
| `FlowForge.Plugin.Tmplt` | `plugins/` | Starter template for building new plugins. |
| `FlowForge.Tests` | `tests/` | Unit, integration, property-based, and E2E tests. |

## Key Concepts

### Workflows

A workflow is a directed graph of nodes connected through typed ports. Every workflow must have exactly one trigger node as its entry point.

### Nodes

Nodes are the building blocks of workflows. Three categories:

| Category | Base Class | Description |
|----------|------------|-------------|
| Trigger | `BaseTriggerNode` | Entry point — webhooks, schedules, manual triggers |
| Action | `BaseActionNode` | Operations — HTTP requests, database queries, email |
| Logic | `BaseLogicNode` | Flow control — conditionals, switches, loops, merges |

### Executions

When a trigger fires, an execution is created and moves through: `Pending` → `Running` → `Completed` / `Failed` / `Cancelled`. Node outputs are stored during execution for use in expressions.

### Expressions

Reference data from previous nodes using double-brace syntax:

```
{{$node.NodeName.data.propertyName}}
{{$execution.id}}
{{$workflow.id}}
```

### Plugins

Extend FlowForge by creating plugin projects that reference `FlowForge.Core` and implement custom nodes.

| Plugin | Description |
|--------|-------------|
| [AdvancedHttp](plugins/FlowForge.Plugin.AdvancedHttp/) | HTTP retry, polling, batch requests, and GraphQL |
| [GitLab](plugins/FlowForge.Plugin.GitLab/) | GitLab issues, merge requests, pipelines, files, tags, releases, and webhook triggers |
| [Jira](plugins/FlowForge.Plugin.Jira/) | Jira issues, comments, users, and JQL search |
| [Template](plugins/FlowForge.Plugin.Tmplt/) | Starter template for building your own plugins |

## Authentication

| Method | Use Case |
|--------|----------|
| JWT Bearer | User sessions from the Designer |
| API Key | Webhooks and external integrations |

## Documentation

Detailed design docs and architectural decisions live in the [`docs/`](docs/) folder.

## License

MIT
