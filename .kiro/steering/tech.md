---
inclusion: always
---

# Vyshyvanka Tech Stack & Conventions

## DO / DON'T

| DO | DON'T |
|----|-------|
| `System.Text.Json` for all serialization | `Newtonsoft.Json` |
| `NSubstitute` for mocking | `Moq` |
| `AwesomeAssertions` for assertions | `FluentAssertions` |
| `xUnit` + `CsCheck` for property tests | NUnit, MSTest |
| Records for DTOs and value objects | Classes with mutable properties for DTOs |
| File-scoped namespaces | Block-scoped namespaces |
| `CancellationToken` in every async method | Fire-and-forget async |
| Return `Task`/`Task<T>` from async | Return `void` from async |
| Primary constructors for DI | Manual field assignment when primary ctor works |
| Collection expressions `[1, 2, 3]` | `new List<int> { 1, 2, 3 }` |
| Raw string literals `"""..."""` for multi-line | Escaped strings or `@""` for multi-line JSON/SQL |
| Pattern matching in switch expressions | Long if-else chains for type/value dispatch |
| Central package versions via `Directory.Packages.props` | Inline `<PackageReference Version="...">` in csproj |

## Runtime

- .NET 10, C# 14, nullable enabled, implicit usings enabled
- ASP.NET Core REST API (`Vyshyvanka.Api`)
- Blazor WebAssembly UI (`Vyshyvanka.Designer`)
- EF Core code-first: SQLite (dev) / PostgreSQL (prod)
- .NET Aspire for dev orchestration (`Vyshyvanka.AppHost`)

## Serialization

Use `System.Text.Json` with camelCase policy everywhere. Prefer source-generated serializer contexts for performance. Only apply `[JsonPropertyName]` when the wire name differs from the C# property name.

```csharp
// Source-generated context
[JsonSerializable(typeof(WorkflowDto))]
public partial class VyshyvankaJsonContext : JsonSerializerContext { }

// Attribute only when name diverges
[JsonPropertyName("workflow_id")]
public string WorkflowId { get; init; }
```

## Async Patterns

- Every async method accepts `CancellationToken` as its last parameter.
- Suffix async methods with `Async`: `ExecuteAsync`, `GetByIdAsync`.
- Always pass `CancellationToken` through to downstream calls.
- Never use `.Result` or `.Wait()` — always `await`.

```csharp
public async Task<Workflow> GetByIdAsync(Guid id, CancellationToken ct)
{
    return await _context.Workflows.FirstOrDefaultAsync(w => w.Id == id, ct)
        ?? throw new WorkflowNotFoundException(id);
}
```

## Error Handling

Domain errors use typed exceptions inheriting `VyshyvankaException` (defined in `Vyshyvanka.Core/Exceptions/`). Each carries an `ErrorCode` string. The `ErrorHandlingMiddleware` maps these to consistent `ApiError` JSON responses with `code`, `message`, `details`, and `traceId`.

- Throw domain-specific exceptions (`WorkflowNotFoundException`, `WorkflowValidationException`, etc.) — don't return error codes manually.
- Never catch exceptions just to rethrow them without adding context.
- Let the middleware handle HTTP status code mapping.

## API Conventions

- Controllers return `ActionResult<T>` with appropriate status codes.
- All error responses use the `ApiError` record: `{ code, message, details?, traceId? }`.
- Validate resource ownership via `ICurrentUserService` before any operation.
- Use `[Authorize]` attributes with policies defined in `Authorization/Policies.cs`.

## EF Core Conventions

- DbContext lives at `Vyshyvanka.Engine/Persistence/VyshyvankaDbContext.cs`.
- Entity classes in `Vyshyvanka.Engine/Persistence/Entities/`.
- Repository interfaces in `Vyshyvanka.Core/Interfaces/`, implementations in `Vyshyvanka.Engine/Persistence/`.
- Always use `async` query methods with `CancellationToken`.
- Use `EnsureCreatedAsync()` for dev; migrations for production schema changes.

## Dependency Injection

- Engine services registered in `Vyshyvanka.Api/Extensions/ServiceCollectionExtensions.cs`.
- API-layer services registered in `Vyshyvanka.Api/Program.cs`.
- Aspire defaults added via `builder.AddServiceDefaults()`.
- Use interface-based registration: `services.AddScoped<IFoo, Foo>()`.
- Authentication and credential storage providers are selected at startup via `appsettings.json` and branched in `ServiceCollectionExtensions`.

## Authentication Providers

Configured via `Authentication:Provider` in `appsettings.json`. Four options:

| Provider | Value | Session Tokens | Login Endpoint |
|----------|-------|---------------|----------------|
| Built-in | `BuiltIn` | Local JWT | Yes |
| Keycloak | `Keycloak` | External OIDC | No (redirect) |
| Authentik | `Authentik` | External OIDC | No (redirect) |
| LDAP | `Ldap` | Local JWT | Yes |

- OIDC providers (Keycloak/Authentik): API validates external tokens; `OidcClaimsTransformation` provisions local users.
- LDAP: `LdapAuthenticationService` verifies credentials against the directory; `LdapAuthService` issues local JWT tokens.
- API key auth (`X-API-Key` header) is always available regardless of provider.
- `GET /api/auth/config` returns the active provider so the Designer can adapt.

## Credential Storage Providers

Configured via `CredentialStorage:Provider` in `appsettings.json`. Three options:

| Provider | Value | Secret Storage | Metadata Storage |
|----------|-------|---------------|-----------------|
| Built-in | `BuiltIn` | AES-256 in DB | DB |
| HashiCorp Vault | `HashiCorpVault` | Vault KV v2 | DB |
| OpenBao | `OpenBao` | OpenBao KV v2 | DB |

- Built-in: `CredentialService` encrypts with `ICredentialEncryption` and persists via `ICredentialRepository`.
- Vault/OpenBao: `VaultCredentialService` stores secrets via `IVaultClient`, metadata via `ICredentialRepository`.
- Validation logic is shared via `CredentialValidator` regardless of backend.

## Testing

All tests live in `Vyshyvanka.Tests/` organized by type: `Unit/`, `Property/`, `Integration/`, `E2E/`.

| Tool | Purpose |
|------|---------|
| xUnit | Test framework (global using via csproj) |
| CsCheck | Property-based testing for validation, serialization, state machines |
| NSubstitute | Mocking interfaces only — never mock concrete classes |
| bUnit | Blazor component tests |
| `Microsoft.AspNetCore.Mvc.Testing` | Integration tests via `WebApplicationFactory` |
| EF Core InMemory | Lightweight DB tests |

Test naming: `When{Condition}Then{ExpectedResult}`

```csharp
[Fact]
public void WhenWorkflowHasNoTriggerThenValidationFails() { }
```

CsCheck property test pattern:

```csharp
[Fact]
public void WhenSerializedThenDeserializationRoundTrips()
{
    Gen.String.Sample(original =>
    {
        var json = JsonSerializer.Serialize(original);
        var result = JsonSerializer.Deserialize<string>(json);
        Assert.Equal(original, result);
    });
}
```

## Package Management

Versions are centralized in `Directory.Packages.props` at the solution root. When adding a dependency:

1. Add `<PackageVersion Include="Pkg" Version="X.Y.Z" />` to `Directory.Packages.props`.
2. Add `<PackageReference Include="Pkg" />` (no version) to the project csproj.

## Commands

```bash
dotnet build                              # Build entire solution
dotnet test                               # Run all tests
dotnet test --filter "FullyQualifiedName~Unit"  # Run unit tests only
dotnet run --project src/Vyshyvanka.Api        # Start API
dotnet run --project src/Vyshyvanka.Designer   # Start Blazor UI
dotnet run --project src/Vyshyvanka.AppHost    # Start via Aspire (all services)
```
