using System.Security.Claims;
using System.Text.Encodings.Web;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Engine.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Vyshyvanka.Tests.Integration.Fixtures;

/// <summary>
/// Custom WebApplicationFactory that replaces SQLite with InMemory DB
/// and uses a fake authentication handler for integration tests.
/// </summary>
public class VyshyvankaWebApplicationFactory : WebApplicationFactory<Vyshyvanka.Api.Program>
{
    public const string TestUserId = "00000000-0000-0000-0000-000000000001";
    public const string TestUserEmail = "test@vyshyvanka.dev";
    public const string TestScheme = "TestScheme";

    private readonly string _dbName = $"VyshyvankaTest_{Guid.NewGuid()}";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Clear the OpenTelemetry logging provider that Aspire adds
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove ALL EF Core / DbContext registrations to avoid dual-provider conflict
            var descriptorsToRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<VyshyvankaDbContext>) ||
                    d.ServiceType == typeof(VyshyvankaDbContext) ||
                    d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true)
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            // Re-add with InMemory provider
            services.AddDbContext<VyshyvankaDbContext>(options => { options.UseInMemoryDatabase(_dbName); });

            // Replace authentication with test scheme
            services.RemoveAll<IConfigureOptions<AuthenticationOptions>>();
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestScheme;
                    options.DefaultChallengeScheme = TestScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestScheme, _ => { });
        });
    }

    /// <summary>
    /// Creates an HttpClient that sends requests as an authenticated admin user.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(
        UserRole role = UserRole.Admin,
        Guid? userId = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", role.ToString());
        if (userId.HasValue)
        {
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId.Value.ToString());
        }

        return client;
    }
}

/// <summary>
/// Authentication handler that always succeeds with configurable claims.
/// </summary>
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers.TryGetValue("X-Test-UserId", out var userIdHeader)
            ? userIdHeader.ToString()
            : VyshyvankaWebApplicationFactory.TestUserId;

        var role = Request.Headers.TryGetValue("X-Test-Role", out var roleHeader)
            ? roleHeader.ToString()
            : "Admin";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, VyshyvankaWebApplicationFactory.TestUserEmail),
            new(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, VyshyvankaWebApplicationFactory.TestScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, VyshyvankaWebApplicationFactory.TestScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
