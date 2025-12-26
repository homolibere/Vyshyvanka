---
inclusion: always
---

# FlowForge Tech Stack

## Framework & Runtime

- .NET 10 with C# 14
- Blazor WebAssembly for visual designer (FlowForge.Designer)
- ASP.NET Core for REST API (FlowForge.Api)
- Prefer modern C# features: records, pattern matching, file-scoped namespaces, raw string literals

## Infrastructure

- SQLite for development/single-instance deployments
- PostgreSQL for production/distributed deployments
- Entity Framework Core for data access (code-first migrations)
- Redis for caching (optional)
- Message queue for distributed execution (optional)

## Serialization

- Use System.Text.Json exclusively (no Newtonsoft.Json)
- Use camelCase naming policy for API responses
- Prefer source-generated JsonSerializerContext for performance
- Use `[JsonPropertyName]` only when property name differs from convention

## Authentication & Security

- JWT Bearer tokens for user authentication
- API key authentication for webhook/external integrations
- AES-256 encryption for stored credentials (via System.Security.Cryptography)
- Never log sensitive data or credentials

## Testing

| Tool | Purpose |
|------|---------|
| xUnit | Primary test framework |
| CsCheck | Property-based testing |
| NSubstitute | Mocking external dependencies |
| TestContainers | Database integration tests |
| WireMock | HTTP service mocking |

**Avoid**: Moq, FluentAssertions (use AwesomeAssertions if assertion library needed)

## Common Commands

```bash
# Build solution
dotnet build

# Run tests
dotnet test

# Run tests with coverage
dotnet tool install -g dotnet-coverage
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test

# Run API
dotnet run --project FlowForge.Api

# Run designer
dotnet run --project FlowForge.Designer
```

## Key Dependencies

| Package | Usage |
|---------|-------|
| System.Text.Json | All JSON serialization |
| Microsoft.AspNetCore.Authentication.JwtBearer | API authentication |
| Microsoft.EntityFrameworkCore.Sqlite | Development database |
| System.Security.Cryptography | AES-256 credential encryption |
