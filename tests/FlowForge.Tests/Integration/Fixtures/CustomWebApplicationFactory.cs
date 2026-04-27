using FlowForge.Engine.Auth;
using FlowForge.Engine.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlowForge.Tests.Integration.Fixtures;

/// <summary>
/// Custom WebApplicationFactory for integration tests.
/// Configures in-memory SQLite database and test-specific settings.
/// </summary>
internal class CustomWebApplicationFactory : WebApplicationFactory<FlowForge.Api.Program>
{
    private readonly string _databaseName;
    private SqliteConnection? _connection;

    public CustomWebApplicationFactory()
    {
        // Generate unique database name for test isolation
        _databaseName = $"TestDb_{Guid.NewGuid():N}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registration
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<FlowForgeDbContext>));
            if (dbContextDescriptor is not null)
            {
                services.Remove(dbContextDescriptor);
            }

            // Remove existing DbContext
            var dbContextServiceDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(FlowForgeDbContext));
            if (dbContextServiceDescriptor is not null)
            {
                services.Remove(dbContextServiceDescriptor);
            }

            // Create and open a persistent SQLite in-memory connection
            // The connection must stay open for the lifetime of the tests
            _connection = new SqliteConnection($"DataSource={_databaseName};Mode=Memory;Cache=Shared");
            _connection.Open();

            // Add DbContext with SQLite in-memory database
            services.AddDbContext<FlowForgeDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // Replace JwtSettings with test-specific settings (shorter expiry)
            var jwtSettingsDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(JwtSettings));
            if (jwtSettingsDescriptor is not null)
            {
                services.Remove(jwtSettingsDescriptor);
            }

            services.AddSingleton(new JwtSettings
            {
                SecretKey = "FlowForgeDefaultSecretKey123456789012345678901234567890",
                Issuer = "FlowForge",
                Audience = "FlowForge",
                AccessTokenExpirationMinutes = 5, // Short expiry for tests
                RefreshTokenExpirationDays = 1
            });

            // Build service provider and ensure database is created
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FlowForgeDbContext>();
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        
        if (disposing)
        {
            _connection?.Close();
            _connection?.Dispose();
        }
    }
}
