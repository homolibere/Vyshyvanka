using Vyshyvanka.Api.Extensions;
using Vyshyvanka.Api.Middleware;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (OpenTelemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add Vyshyvanka services
builder.Services.AddVyshyvankaServices(builder.Configuration);

// Add API services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Enter your JWT token"
    });

    options.AddSecurityRequirement(_ => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer")] = new List<string>()
    });
});

// Add CORS for Blazor WebAssembly client
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Ensure database is created with current schema
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<Vyshyvanka.Engine.Persistence.VyshyvankaDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

// Initialize NuGet package manager on startup
await InitializePackageManagerAsync(app.Services);

// Seed development users in development environment
if (app.Environment.IsDevelopment())
{
    await DevelopmentUserSeeder.SeedDevelopmentUsersAsync(app.Services);
}

// Configure the HTTP request pipeline
app.UseErrorHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Map Aspire health check endpoints (/health and /alive)
app.MapDefaultEndpoints();

app.Run();

// Initializes the NuGet package manager by loading installed packages from the manifest.
static async Task InitializePackageManagerAsync(IServiceProvider services)
{
    var logger = services.GetService<ILogger<Program>>();
    var packageManager = services.GetService<INuGetPackageManager>();

    if (packageManager is null)
    {
        logger?.LogWarning("NuGet package manager not registered, skipping initialization");
        return;
    }

    try
    {
        logger?.LogInformation("Initializing NuGet package manager...");
        await packageManager.InitializeAsync();
        logger?.LogInformation("NuGet package manager initialized successfully");
    }
    catch (Exception ex)
    {
        logger?.LogError(ex, "Failed to initialize NuGet package manager");
        // Don't throw - allow the application to start even if package manager fails
    }
}

namespace Vyshyvanka.Api
{
    /// <summary>
    /// Partial class declaration to make Program accessible for integration tests.
    /// </summary>
    public partial class Program;
}
