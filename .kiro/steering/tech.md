---
inclusion: always
---

# FlowForge Tech Stack

## Quick Reference

| DO | DON'T |
|----|-------|
| Use `System.Text.Json` for all serialization | Use `Newtonsoft.Json` |
| Use `NSubstitute` for mocking | Use `Moq` |
| Use `AwesomeAssertions` if needed | Use `FluentAssertions` |
| Use `xUnit` with `CsCheck` for property tests | Use NUnit or MSTest |
| Use records for DTOs | Use classes with mutable properties |
| Use file-scoped namespaces | Use block-scoped namespaces |
| Use `CancellationToken` in all async methods | Fire-and-forget async calls |
| Return `Task`/`Task<T>` from async methods | Return `void` from async methods |

## Framework & Runtime

- **.NET 10** with **C# 14**
- **Blazor WebAssembly**: `FlowForge.Designer` (visual workflow editor)
- **ASP.NET Core**: `FlowForge.Api` (REST API)
- **Entity Framework Core**: Code-first with SQLite (dev) / PostgreSQL (prod)

### C# 14 Features to Use

- Records for immutable types and DTOs
- Pattern matching in switch expressions
- File-scoped namespaces (single line)
- Raw string literals (`"""`) for multi-line JSON/SQL
- Collection expressions (`[1, 2, 3]`)
- Primary constructors where appropriate

## Serialization

```csharp
// CORRECT: Use System.Text.Json with camelCase
var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

// CORRECT: Source-generated context for performance
[JsonSerializable(typeof(WorkflowDto))]
public partial class FlowForgeJsonContext : JsonSerializerContext { }

// CORRECT: Only use attribute when name differs
[JsonPropertyName("workflow_id")]  // Only if API requires snake_case
public string WorkflowId { get; init; }
```

## Authentication & Security

| Mechanism | Use Case |
|-----------|----------|
| JWT Bearer | User authentication via `FlowForge.Designer` |
| API Key | Webhooks and external integrations |
| AES-256 | Credential encryption at rest |

**Rules:**
- Never log credentials or sensitive data
- Always validate user owns resource before operations
- Use `ICurrentUserService` to get authenticated user context

## Testing

| Tool | Purpose | When to Use |
|------|---------|-------------|
| xUnit | Test framework | All tests |
| CsCheck | Property-based testing | Validation, serialization, state transitions |
| NSubstitute | Mocking | External dependencies only |
| TestContainers | Integration tests | Database operations |
| WireMock | HTTP mocking | External API calls |

**Test Naming**: `When{Condition}Then{ExpectedResult}`
```csharp
// CORRECT
public void WhenWorkflowHasNoTriggerThenValidationFails() { }

// INCORRECT
public void TestValidation() { }
```

## Commands

```bash
dotnet build                              # Build solution
dotnet test                               # Run all tests
dotnet run --project FlowForge.Api        # Start API server
dotnet run --project FlowForge.Designer   # Start Blazor designer
```

## Key Dependencies

| Package | Purpose |
|---------|---------|
| `System.Text.Json` | JSON serialization |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT auth |
| `Microsoft.EntityFrameworkCore.Sqlite` | Dev database |
| `System.Security.Cryptography` | AES-256 encryption |
