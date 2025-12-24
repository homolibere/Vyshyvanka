# FlowForge Tech Stack

## Framework & Runtime

- .NET 10 (C# 14)
- Blazor WebAssembly for visual designer
- ASP.NET Core for REST API

## Project Structure

- `FlowForge.Core` - Domain models and interfaces
- `FlowForge.Engine` - Workflow execution engine
- `FlowForge.Api` - REST API and webhooks
- `FlowForge.Designer` - Blazor WebAssembly UI
- `FlowForge.Tests` - Test project

## Infrastructure

- SQLite (preferred for development/single-instance) or PostgreSQL (for production/distributed)
- Entity Framework Core for data access
- Redis for caching (optional)
- Message queue for distributed execution (optional)

## Testing

- xUnit as primary test framework
- CsCheck for property-based testing
- NSubstitute for mocking
- TestContainers for database integration tests
- WireMock for HTTP service mocking

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

- System.Text.Json for serialization
- Microsoft.AspNetCore.Authentication.JwtBearer for auth
- System.Security.Cryptography for AES-256 encryption
