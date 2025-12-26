using System.Net;
using System.Net.Http.Json;
using FlowForge.Api.Controllers;
using FlowForge.Tests.Integration.Fixtures;

namespace FlowForge.Tests.Integration.Auth;

/// <summary>
/// Integration tests for the AuthController endpoints.
/// Tests login, registration, and token refresh functionality.
/// </summary>
public class AuthControllerTests : IClassFixture<FlowForgeApiFixture>
{
    private readonly FlowForgeApiFixture _fixture;
    private readonly HttpClient _client;

    public AuthControllerTests(FlowForgeApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.AnonymousClient;
    }

    #region Login Tests (Task 2.1)

    [Fact]
    public async Task WhenValidCredentialsThenReturnsAccessToken()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = FlowForgeApiFixture.TestUsers.Editor.Email,
            Password = FlowForgeApiFixture.TestUsers.Editor.Password
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginResponse);
        Assert.False(string.IsNullOrEmpty(loginResponse.AccessToken));
        Assert.False(string.IsNullOrEmpty(loginResponse.RefreshToken));
        Assert.True(loginResponse.ExpiresAt > DateTime.UtcNow);
        Assert.NotNull(loginResponse.User);
        Assert.Equal(FlowForgeApiFixture.TestUsers.Editor.Email, loginResponse.User.Email);
    }

    [Fact]
    public async Task WhenInvalidPasswordThenReturns401Unauthorized()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = FlowForgeApiFixture.TestUsers.Editor.Email,
            Password = "WrongPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WhenNonExistentEmailThenReturns401Unauthorized()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "nonexistent@test.com",
            Password = "SomePassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WhenEmptyCredentialsThenReturns400BadRequest()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "",
            Password = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WhenEmptyEmailThenReturns400BadRequest()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "",
            Password = "SomePassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WhenEmptyPasswordThenReturns400BadRequest()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = FlowForgeApiFixture.TestUsers.Editor.Email,
            Password = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion


    #region Registration Tests (Task 2.2)

    [Fact]
    public async Task WhenValidRegistrationThenReturnsAccessToken()
    {
        // Arrange
        var uniqueEmail = $"newuser_{Guid.NewGuid():N}@test.com";
        var request = new RegisterRequest
        {
            Email = uniqueEmail,
            Password = "NewUserPass123!",
            DisplayName = "New Test User"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginResponse);
        Assert.False(string.IsNullOrEmpty(loginResponse.AccessToken));
        Assert.False(string.IsNullOrEmpty(loginResponse.RefreshToken));
        Assert.True(loginResponse.ExpiresAt > DateTime.UtcNow);
        Assert.NotNull(loginResponse.User);
        Assert.Equal(uniqueEmail, loginResponse.User.Email);
        Assert.Equal("New Test User", loginResponse.User.DisplayName);
    }

    [Fact]
    public async Task WhenDuplicateEmailThenReturns400BadRequest()
    {
        // Arrange - Use an existing test user email
        var request = new RegisterRequest
        {
            Email = FlowForgeApiFixture.TestUsers.Editor.Email,
            Password = "AnotherPass123!",
            DisplayName = "Duplicate User"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WhenEmptyRegistrationCredentialsThenReturns400BadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "",
            Password = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Token Refresh Tests (Task 2.3)

    [Fact]
    public async Task WhenValidRefreshTokenThenReturnsNewAccessToken()
    {
        // Arrange - First login to get a valid refresh token
        var loginRequest = new LoginRequest
        {
            Email = FlowForgeApiFixture.TestUsers.Editor.Email,
            Password = FlowForgeApiFixture.TestUsers.Editor.Password
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginResult);

        var refreshRequest = new RefreshRequest
        {
            RefreshToken = loginResult.RefreshToken
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var refreshResult = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(refreshResult);
        Assert.False(string.IsNullOrEmpty(refreshResult.AccessToken));
        Assert.False(string.IsNullOrEmpty(refreshResult.RefreshToken));
        Assert.True(refreshResult.ExpiresAt > DateTime.UtcNow);
        Assert.NotNull(refreshResult.User);
    }

    [Fact]
    public async Task WhenInvalidRefreshTokenThenReturns401Unauthorized()
    {
        // Arrange
        var request = new RefreshRequest
        {
            RefreshToken = "invalid-refresh-token"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WhenEmptyRefreshTokenThenReturns400BadRequest()
    {
        // Arrange
        var request = new RefreshRequest
        {
            RefreshToken = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion
}
