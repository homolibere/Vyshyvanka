using FlowForge.Api.Extensions;
using FlowForge.Api.Middleware;
using FlowForge.Core.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add FlowForge services
builder.Services.AddFlowForgeServices(builder.Configuration);

// Add API services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Initialize NuGet package manager on startup
await InitializePackageManagerAsync(app.Services);

// Configure the HTTP request pipeline
app.UseErrorHandling();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

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
