var builder = DistributedApplication.CreateBuilder(args);

var databaseProvider = builder.Configuration["Database:Provider"]
    ?? Environment.GetEnvironmentVariable("Database__Provider")
    ?? "PostgreSql";

var existingConnectionString = builder.Configuration["ConnectionStrings:vyshyvankadb"]
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__vyshyvankadb");

IResourceBuilder<ProjectResource> api;

if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
{
    var connStr = existingConnectionString ?? "Data Source=vyshyvanka.db";
    Console.WriteLine($"[Vyshyvanka] Database: SQLite ({connStr})");

    api = builder.AddProject<Projects.Vyshyvanka_Api>("api")
        .WithEnvironment("Database__Provider", "Sqlite")
        .WithEnvironment("ConnectionStrings__vyshyvankadb", connStr);
}
else if (!string.IsNullOrWhiteSpace(existingConnectionString))
{
    Console.WriteLine($"[Vyshyvanka] Database: PostgreSQL (existing instance)");

    var database = builder.AddConnectionString("vyshyvankadb");

    api = builder.AddProject<Projects.Vyshyvanka_Api>("api")
        .WithReference(database)
        .WithEnvironment("Database__Provider", "PostgreSql");
}
else
{
    Console.WriteLine("[Vyshyvanka] Database: PostgreSQL (Aspire-managed container)");

    var postgres = builder.AddPostgres("postgres")
        .WithDataVolume("vyshyvanka-postgres-data");

    var database = postgres.AddDatabase("vyshyvankadb");

    api = builder.AddProject<Projects.Vyshyvanka_Api>("api")
        .WithReference(database)
        .WaitFor(database)
        .WithEnvironment("Database__Provider", "PostgreSql");
}

// Add Designer service with reference to API for service discovery
builder.AddProject<Projects.Vyshyvanka_Designer>("designer")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
