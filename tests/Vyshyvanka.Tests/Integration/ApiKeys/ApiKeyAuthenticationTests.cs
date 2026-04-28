using System.Net;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Tests.Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Vyshyvanka.Tests.Integration.ApiKeys;

/// <summary>
/// Integration tests for API key authentication.
/// Tests valid, invalid, expired, and revoked API key scenarios.
/// </summary>
public class ApiKeyAuthenticationTests : IClassFixture<VyshyvankaApiFixture>, IAsyncLifetime
{
    private readonly VyshyvankaApiFixture _fixture;

    public ApiKeyAuthenticationTests(VyshyvankaApiFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    #region Valid API Key Tests (Requirement 7.1)

    [Fact]
    public async Task WhenValidApiKeyThenAuthenticatesRequest()
    {
        // Arrange
        using var client = await _fixture.CreateApiKeyClientAsync("valid-test-key");

        // Act
        var response = await client.GetAsync("/api/workflow");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WhenValidApiKeyWithScopesThenAuthenticatesRequest()
    {
        // Arrange
        using var client = await _fixture.CreateApiKeyClientAsync(
            "scoped-test-key",
            scopes: ["workflow:read", "workflow:write"]);

        // Act
        var response = await client.GetAsync("/api/workflow");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region Invalid API Key Tests (Requirement 7.2)

    [Fact]
    public async Task WhenInvalidApiKeyThenReturns401Unauthorized()
    {
        // Arrange
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "invalid-api-key-that-does-not-exist");

        // Act
        var response = await client.GetAsync("/api/workflow");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WhenEmptyApiKeyThenReturns401Unauthorized()
    {
        // Arrange
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "");

        // Act
        var response = await client.GetAsync("/api/workflow");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WhenMalformedApiKeyThenReturns401Unauthorized()
    {
        // Arrange
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "not_a_valid_format_key");

        // Act
        var response = await client.GetAsync("/api/workflow");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Expired API Key Tests (Requirement 7.3)

    [Fact]
    public async Task WhenExpiredApiKeyThenReturns401Unauthorized()
    {
        // Arrange - Create an API key that expired in the past
        var client = _fixture.Factory.CreateClient();
        
        using var scope = _fixture.Factory.Services.CreateScope();
        var apiKeyService = scope.ServiceProvider.GetRequiredService<IApiKeyService>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var user = await userRepository.GetByEmailAsync(VyshyvankaApiFixture.TestUsers.Editor.Email);
        Assert.NotNull(user);

        // Create an API key that expired 1 hour ago
        var result = await apiKeyService.CreateAsync(
            user.Id,
            "expired-test-key",
            scopes: null,
            expiresAt: DateTime.UtcNow.AddHours(-1));

        Assert.True(result.Success);
        Assert.NotNull(result.PlainTextKey);

        client.DefaultRequestHeaders.Add("X-API-Key", result.PlainTextKey);

        // Act
        var response = await client.GetAsync("/api/workflow");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WhenApiKeyExpiresJustNowThenReturns401Unauthorized()
    {
        // Arrange - Create an API key that expired just now
        var client = _fixture.Factory.CreateClient();
        
        using var scope = _fixture.Factory.Services.CreateScope();
        var apiKeyService = scope.ServiceProvider.GetRequiredService<IApiKeyService>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var user = await userRepository.GetByEmailAsync(VyshyvankaApiFixture.TestUsers.Editor.Email);
        Assert.NotNull(user);

        // Create an API key that expired 1 second ago
        var result = await apiKeyService.CreateAsync(
            user.Id,
            "just-expired-test-key",
            scopes: null,
            expiresAt: DateTime.UtcNow.AddSeconds(-1));

        Assert.True(result.Success);
        Assert.NotNull(result.PlainTextKey);

        client.DefaultRequestHeaders.Add("X-API-Key", result.PlainTextKey);

        // Act
        var response = await client.GetAsync("/api/workflow");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Revoked API Key Tests (Requirement 7.4)

    [Fact]
    public async Task WhenRevokedApiKeyThenReturns401Unauthorized()
    {
        // Arrange - Create an API key and then revoke it
        var client = _fixture.Factory.CreateClient();
        
        using var scope = _fixture.Factory.Services.CreateScope();
        var apiKeyService = scope.ServiceProvider.GetRequiredService<IApiKeyService>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var user = await userRepository.GetByEmailAsync(VyshyvankaApiFixture.TestUsers.Editor.Email);
        Assert.NotNull(user);

        // Create an API key
        var result = await apiKeyService.CreateAsync(
            user.Id,
            "revoked-test-key",
            scopes: null,
            expiresAt: null);

        Assert.True(result.Success);
        Assert.NotNull(result.PlainTextKey);
        Assert.NotNull(result.ApiKey);

        // Revoke the API key
        await apiKeyService.RevokeAsync(result.ApiKey.Id);

        client.DefaultRequestHeaders.Add("X-API-Key", result.PlainTextKey);

        // Act
        var response = await client.GetAsync("/api/workflow");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WhenApiKeyRevokedAfterCreationThenReturns401Unauthorized()
    {
        // Arrange - Create an API key, verify it works, then revoke it
        using var scope = _fixture.Factory.Services.CreateScope();
        var apiKeyService = scope.ServiceProvider.GetRequiredService<IApiKeyService>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var user = await userRepository.GetByEmailAsync(VyshyvankaApiFixture.TestUsers.Editor.Email);
        Assert.NotNull(user);

        // Create an API key
        var result = await apiKeyService.CreateAsync(
            user.Id,
            "revoke-after-use-key",
            scopes: null,
            expiresAt: null);

        Assert.True(result.Success);
        Assert.NotNull(result.PlainTextKey);
        Assert.NotNull(result.ApiKey);

        // First verify the key works
        var clientBeforeRevoke = _fixture.Factory.CreateClient();
        clientBeforeRevoke.DefaultRequestHeaders.Add("X-API-Key", result.PlainTextKey);
        var responseBeforeRevoke = await clientBeforeRevoke.GetAsync("/api/workflow");
        Assert.Equal(HttpStatusCode.OK, responseBeforeRevoke.StatusCode);

        // Now revoke the API key
        await apiKeyService.RevokeAsync(result.ApiKey.Id);

        // Create a new client with the same key (after revocation)
        var clientAfterRevoke = _fixture.Factory.CreateClient();
        clientAfterRevoke.DefaultRequestHeaders.Add("X-API-Key", result.PlainTextKey);

        // Act
        var responseAfterRevoke = await clientAfterRevoke.GetAsync("/api/workflow");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, responseAfterRevoke.StatusCode);
    }

    #endregion
}
