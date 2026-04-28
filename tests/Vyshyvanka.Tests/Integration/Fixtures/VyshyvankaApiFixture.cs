using System.Net.Http.Headers;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Engine.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Vyshyvanka.Tests.Integration.Fixtures;

/// <summary>
/// Test fixture providing shared test server and client factories for integration tests.
/// Implements IAsyncLifetime for async setup/teardown.
/// </summary>
public class VyshyvankaApiFixture : IAsyncLifetime
{
    private CustomWebApplicationFactory _factory = null!;

    /// <summary>
    /// Gets the WebApplicationFactory for creating clients.
    /// </summary>
    public WebApplicationFactory<Vyshyvanka.Api.Program> Factory => _factory;

    /// <summary>
    /// Gets an HttpClient without authentication headers for testing public endpoints.
    /// </summary>
    public HttpClient AnonymousClient { get; private set; } = null!;

    /// <summary>
    /// Test users seeded during initialization.
    /// </summary>
    public static class TestUsers
    {
        public static readonly TestUser Admin = new("admin@test.com", "AdminPass123!", UserRole.Admin);
        public static readonly TestUser Editor = new("editor@test.com", "EditorPass123!", UserRole.Editor);
        public static readonly TestUser Viewer = new("viewer@test.com", "ViewerPass123!", UserRole.Viewer);
    }

    public async Task InitializeAsync()
    {
        _factory = new CustomWebApplicationFactory();
        AnonymousClient = _factory.CreateClient();

        // Seed test users
        await SeedTestUsersAsync();
    }

    public async Task DisposeAsync()
    {
        AnonymousClient.Dispose();
        await _factory.DisposeAsync();
    }

    /// <summary>
    /// Creates an authenticated HttpClient with a valid JWT token for the specified user.
    /// </summary>
    /// <param name="email">User email (defaults to editor@test.com)</param>
    /// <param name="password">User password (defaults to EditorPass123!)</param>
    /// <param name="role">User role (defaults to Editor)</param>
    /// <returns>HttpClient configured with Bearer token authentication</returns>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(
        string email = "editor@test.com",
        string password = "EditorPass123!",
        UserRole role = UserRole.Editor)
    {
        var client = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var result = await authService.LoginAsync(email, password);
        if (!result.Success || string.IsNullOrEmpty(result.AccessToken))
        {
            throw new InvalidOperationException($"Failed to authenticate user {email}: {result.ErrorMessage}");
        }

        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", result.AccessToken);

        return client;
    }

    /// <summary>
    /// Creates an HttpClient authenticated with an API key.
    /// </summary>
    /// <param name="name">API key name</param>
    /// <param name="scopes">Optional scopes for the API key</param>
    /// <param name="expiresAt">Optional expiration date</param>
    /// <returns>HttpClient configured with X-API-Key header</returns>
    public async Task<HttpClient> CreateApiKeyClientAsync(
        string name = "test-key",
        string[]? scopes = null,
        DateTime? expiresAt = null)
    {
        var client = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var apiKeyService = scope.ServiceProvider.GetRequiredService<IApiKeyService>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        // Get the editor user to associate the API key with
        var user = await userRepository.GetByEmailAsync(TestUsers.Editor.Email);
        if (user is null)
        {
            throw new InvalidOperationException("Test user not found. Ensure InitializeAsync was called.");
        }

        var result = await apiKeyService.CreateAsync(
            user.Id,
            name,
            scopes?.ToList(),
            expiresAt);

        if (!result.Success || string.IsNullOrEmpty(result.PlainTextKey))
        {
            throw new InvalidOperationException($"Failed to create API key: {result.ErrorMessage}");
        }

        client.DefaultRequestHeaders.Add("X-API-Key", result.PlainTextKey);

        return client;
    }

    /// <summary>
    /// Gets a service from the test server's service provider.
    /// </summary>
    public T GetService<T>() where T : class
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Gets a scoped service from the test server's service provider.
    /// Returns the scope so caller can dispose it properly.
    /// </summary>
    public (IServiceScope Scope, T Service) GetScopedService<T>() where T : class
    {
        var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<T>();
        return (scope, service);
    }

    private async Task SeedTestUsersAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<VyshyvankaDbContext>();

        // Seed Admin user
        await SeedUserAsync(userRepository, authService, TestUsers.Admin);
        
        // Seed Editor user
        await SeedUserAsync(userRepository, authService, TestUsers.Editor);
        
        // Seed Viewer user
        await SeedUserAsync(userRepository, authService, TestUsers.Viewer);

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedUserAsync(
        IUserRepository userRepository,
        IAuthService authService,
        TestUser testUser)
    {
        var existingUser = await userRepository.GetByEmailAsync(testUser.Email);
        if (existingUser is not null)
        {
            return;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = testUser.Email,
            DisplayName = testUser.Email.Split('@')[0],
            PasswordHash = authService.HashPassword(testUser.Password),
            Role = testUser.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await userRepository.CreateAsync(user);
    }
}

/// <summary>
/// Represents a test user with credentials and role.
/// </summary>
public record TestUser(string Email, string Password, UserRole Role);
