using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Check if PostgreSQL mode is enabled via environment variable
var usePostgres = builder.Configuration["UsePostgres"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
    || Environment.GetEnvironmentVariable("USE_POSTGRES")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

IResourceBuilder<ProjectResource> api;

if (usePostgres)
{
    // Configure PostgreSQL for production/distributed deployments
    var postgres = builder.AddPostgres("postgres")
        .WithDataVolume("flowforge-postgres-data");

    var database = postgres.AddDatabase("flowforgedb");

    // Add API service with PostgreSQL connection
    api = builder.AddProject<Projects.FlowForge_Api>("api")
        .WithReference(database)
        .WaitFor(database);
}
else
{
    // Default: SQLite for development/single-instance deployments
    api = builder.AddProject<Projects.FlowForge_Api>("api");
}

// Add Designer service with reference to API for service discovery
builder.AddProject<Projects.FlowForge_Designer>("designer")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
