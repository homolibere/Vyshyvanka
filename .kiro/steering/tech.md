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
| `CultureInfo.InvariantCulture` for SVG/number formatting | Default culture-dependent `ToString()` or `$"{value}"` in markup/SVG |
| `FormattableString.Invariant($"...")` for SVG path/attribute strings | Plain `$"..."` interpolation with doubles in SVG context |

## Runtime

- .NET 10, C# 14, nullable enabled, implicit usings enabled
- ASP.NET Core REST API (`Vyshyvanka.Api`)
- Blazor WebAssembly UI (`Vyshyvanka.Designer`)
- EF Core code-first: PostgreSQL (default) / SQLite (opt-in for CI)
- .NET Aspire for dev orchestration (`Vyshyvanka.AppHost`)

## Culture & Locale

The Designer sets `CultureInfo.InvariantCulture` globally at startup (`Program.cs`). This ensures SVG attributes always use `.` as the decimal separator regardless of the user's browser locale.

When formatting numbers for SVG output (viewBox, transform, path `d`, width/height):
- Use `FormattableString.Invariant($"...")` in code-behind methods.
- Never use plain `$"{doubleValue}"` — it respects `CurrentCulture` and produces `,` on European locales, breaking SVG.

```csharp
// Correct — always produces "translate(144.5, 4)"
return FormattableString.Invariant($"translate({x}, {y})");

// Wrong — produces "translate(144,5, 4)" on comma-locale systems
return $"translate({x}, {y})";
```

## Theming

The Designer uses a JSON-based theming system. Themes are stored as static JSON files in `wwwroot/themes/` and applied at runtime via CSS custom properties through JS interop.

**Theme JSON structure:**
- `id`, `name`, `baseMode` ("light"/"dark"), `description`, `author`
- `preview` — three color swatches for the selector UI
- `colors` — maps CSS variable names (without `--` prefix) to values
- `icons` — maps icon keys to CSS classes (e.g. `"trigger": "fa-solid fa-bolt"`)
- `canvas` — `pattern`: "vyshyvanka", "dots", or "none"

**Built-in themes:** `vyshyvanka-light`, `vyshyvanka-dark`, `slate`, `ocean-dark`, `minimal`

**Custom themes:** Users upload JSON files via Settings. Stored in localStorage (`vyshyvanka-custom-themes`). Active theme ID persisted in `vyshyvanka-active-theme`.

**ThemeService API:**
- `InitializeAsync()` — loads built-in + custom themes, applies saved active
- `SetThemeAsync(themeId)` — switch theme
- `ImportThemeAsync(json)` — add custom theme
- `RemoveThemeAsync(themeId)` — delete custom theme
- `GetIcon(key)` — resolve icon class from active theme
- `IsVyshyvankaPattern` / `CanvasPattern` — canvas pattern from active theme

`theme.css` remains as a fallback for initial paint before JS applies the active theme.

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
- Migrations in `Vyshyvanka.Engine/Persistence/Migrations/`.
- Repository interfaces in `Vyshyvanka.Core/Interfaces/`, implementations in `Vyshyvanka.Engine/Persistence/`.
- Always use `async` query methods with `CancellationToken`.
- Use EF Core migrations for ALL schema changes. Never use `EnsureCreatedAsync()`.
- At startup, `MigrateAsync()` applies pending migrations automatically.
- To add a migration: `dotnet ef migrations add <Name> --project src/Vyshyvanka.Engine --startup-project src/Vyshyvanka.Api --output-dir Persistence/Migrations`

## Database Provider

Configured via `Database:Provider` in `appsettings.json`. Two options:

| Provider | Value | Connection String | Use Case |
|----------|-------|-------------------|----------|
| PostgreSQL | `PostgreSql` | Standard Npgsql connection string | Default — dev and production |
| SQLite | `Sqlite` | `Data Source=vyshyvanka.db` | Lightweight/CI scenarios |

- Connection string name is always `vyshyvankadb` (in `ConnectionStrings` section).
- PostgreSQL is the default. The Aspire AppHost detects an existing connection string or spins up a container automatically.
- To use an existing PostgreSQL instance: set `ConnectionStrings:vyshyvankadb` in the AppHost's `appsettings.Development.json` or via env var `ConnectionStrings__vyshyvankadb`.
- For SQLite (no container needed): set `Database:Provider` to `Sqlite` in AppHost config or via env var `Database__Provider=Sqlite`.
- Migrations are provider-agnostic: column definitions omit the `type:` parameter so EF Core resolves store types from CLR types at apply time. This lets one migration set work with both PostgreSQL and SQLite.
- The `DesignTimeDbContextFactory` targets PostgreSQL for snapshot/diff purposes during `dotnet ef migrations add`. The generated `.cs` migration files must be post-processed to remove `type:` parameters and provider-specific annotations before commit.
- When adding a new migration, always strip `type: "..."` from column definitions and remove `using Npgsql...Metadata;` / `.Annotation("Npgsql:...", ...)` lines from the `.cs` file.
- `PendingModelChangesWarning` is suppressed in DbContext registration because the ModelSnapshot uses PostgreSQL annotations that differ from the runtime provider. This is expected — do not remove the suppression.

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
dotnet ef migrations add <Name> --project src/Vyshyvanka.Engine --startup-project src/Vyshyvanka.Api --output-dir Persistence/Migrations  # Add migration
dotnet ef migrations remove --project src/Vyshyvanka.Engine --startup-project src/Vyshyvanka.Api  # Remove last migration
```
