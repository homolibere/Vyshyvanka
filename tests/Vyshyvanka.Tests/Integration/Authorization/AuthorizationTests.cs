using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Vyshyvanka.Api.Models;
using Vyshyvanka.Tests.Integration.Fixtures;
using Microsoft.IdentityModel.Tokens;

namespace Vyshyvanka.Tests.Integration.Authorization;

/// <summary>
/// Integration tests for authorization enforcement.
/// Tests role-based access control and token validation.
/// </summary>
public class AuthorizationTests : IClassFixture<VyshyvankaApiFixture>, IAsyncLifetime
{
    private readonly VyshyvankaApiFixture _fixture;
    private HttpClient _viewerClient = null!;
    private HttpClient _editorClient = null!;

    public AuthorizationTests(VyshyvankaApiFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _viewerClient = await _fixture.CreateAuthenticatedClientAsync(
            VyshyvankaApiFixture.TestUsers.Viewer.Email,
            VyshyvankaApiFixture.TestUsers.Viewer.Password);

        _editorClient = await _fixture.CreateAuthenticatedClientAsync(
            VyshyvankaApiFixture.TestUsers.Editor.Email,
            VyshyvankaApiFixture.TestUsers.Editor.Password);
    }

    public Task DisposeAsync()
    {
        _viewerClient.Dispose();
        _editorClient.Dispose();
        return Task.CompletedTask;
    }

    #region Anonymous Client Tests (Task 6.1)

    [Fact]
    public async Task WhenAnonymousClientCallsProtectedEndpointThenReturns401Unauthorized()
    {
        // Arrange
        var anonymousClient = _fixture.AnonymousClient;

        // Act
        var response = await anonymousClient.GetAsync("/api/workflow");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WhenAnonymousClientCallsPostWorkflowThenReturns401Unauthorized()
    {
        // Arrange
        var anonymousClient = _fixture.AnonymousClient;
        var request = TestDataFactory.CreateValidWorkflow("Anonymous Test Workflow");

        // Act
        var response = await anonymousClient.PostAsJsonAsync("/api/workflow", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WhenAnonymousClientCallsGetWorkflowByIdThenReturns401Unauthorized()
    {
        // Arrange
        var anonymousClient = _fixture.AnonymousClient;
        var workflowId = Guid.NewGuid();

        // Act
        var response = await anonymousClient.GetAsync($"/api/workflow/{workflowId}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Role-Based Access Tests (Task 6.1)

    [Fact]
    public async Task WhenViewerRoleCallsPostWorkflowThenReturns403Forbidden()
    {
        // Arrange
        var request = TestDataFactory.CreateValidWorkflow("Viewer Test Workflow");

        // Act
        var response = await _viewerClient.PostAsJsonAsync("/api/workflow", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WhenViewerRoleCallsGetWorkflowsThenReturnsSuccess()
    {
        // Arrange & Act
        var response = await _viewerClient.GetAsync("/api/workflow");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WhenEditorRoleCallsPostWorkflowThenReturnsSuccess()
    {
        // Arrange
        var request = TestDataFactory.CreateValidWorkflow($"Editor Test Workflow {Guid.NewGuid():N}");

        // Act
        var response = await _editorClient.PostAsJsonAsync("/api/workflow", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var workflow = await response.Content.ReadFromJsonAsync<WorkflowResponse>();
        Assert.NotNull(workflow);
        Assert.Equal(request.Name, workflow.Name);
    }

    [Fact]
    public async Task WhenViewerRoleCallsDeleteWorkflowThenReturns403Forbidden()
    {
        // Arrange - First create a workflow with editor
        var createRequest = TestDataFactory.CreateValidWorkflow($"Delete Test {Guid.NewGuid():N}");
        var createResponse = await _editorClient.PostAsJsonAsync("/api/workflow", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();
        Assert.NotNull(created);

        // Act - Try to delete with viewer
        var response = await _viewerClient.DeleteAsync($"/api/workflow/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WhenViewerRoleCallsPutWorkflowThenReturns403Forbidden()
    {
        // Arrange - First create a workflow with editor
        var createRequest = TestDataFactory.CreateValidWorkflow($"Update Test {Guid.NewGuid():N}");
        var createResponse = await _editorClient.PostAsJsonAsync("/api/workflow", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();
        Assert.NotNull(created);

        var updateRequest = TestDataFactory.CreateUpdateRequest(created, "Updated Name");

        // Act - Try to update with viewer
        var response = await _viewerClient.PutAsJsonAsync($"/api/workflow/{created.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region Token Validation Tests (Task 6.2)

    [Fact]
    public async Task WhenExpiredTokenThenReturns401Unauthorized()
    {
        // Arrange - Create a token that expired in the past
        var expiredToken = GenerateExpiredToken();
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        // Act
        var response = await client.GetAsync("/api/workflow");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WhenInvalidSignatureThenReturns401Unauthorized()
    {
        // Arrange - Create a token with a different secret key
        var invalidToken = GenerateTokenWithInvalidSignature();
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalidToken);

        // Act
        var response = await client.GetAsync("/api/workflow");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WhenMalformedTokenThenReturns401Unauthorized()
    {
        // Arrange
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-jwt-token");

        // Act
        var response = await client.GetAsync("/api/workflow");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WhenEmptyBearerTokenThenReturns401Unauthorized()
    {
        // Arrange
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "");

        // Act
        var response = await client.GetAsync("/api/workflow");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Property Tests (Task 6.3)

    /// <summary>
    /// Property 2: Authenticated Client Authorization
    /// For any valid user credentials (email, password, role), the Authenticated_Client created by 
    /// the Test_Fixture SHALL successfully receive 2xx responses when calling endpoints permitted for that role.
    /// Validates: Requirements 1.3
    /// </summary>
    [Fact]
    public async Task WhenAuthenticatedClientCallsPermittedEndpointsThenReceives2xxResponses()
    {
        // Feature: api-integration-tests, Property 2: Authenticated Client Authorization
        // Validates: Requirements 1.3

        // Define test users with their permitted endpoints
        var testCases = new List<(TestUser User, string Endpoint, HttpMethod Method, Func<HttpClient, Task<HttpResponseMessage>> Request)>
        {
            // Viewer can view workflows (GET)
            (VyshyvankaApiFixture.TestUsers.Viewer, "/api/workflow", HttpMethod.Get, 
                client => client.GetAsync("/api/workflow")),
            
            // Editor can view workflows (GET)
            (VyshyvankaApiFixture.TestUsers.Editor, "/api/workflow", HttpMethod.Get, 
                client => client.GetAsync("/api/workflow")),
            
            // Admin can view workflows (GET)
            (VyshyvankaApiFixture.TestUsers.Admin, "/api/workflow", HttpMethod.Get, 
                client => client.GetAsync("/api/workflow")),
            
            // Editor can create workflows (POST)
            (VyshyvankaApiFixture.TestUsers.Editor, "/api/workflow", HttpMethod.Post, 
                client => client.PostAsJsonAsync("/api/workflow", TestDataFactory.CreateValidWorkflow($"PropertyTest_{Guid.NewGuid():N}"))),
            
            // Admin can create workflows (POST)
            (VyshyvankaApiFixture.TestUsers.Admin, "/api/workflow", HttpMethod.Post, 
                client => client.PostAsJsonAsync("/api/workflow", TestDataFactory.CreateValidWorkflow($"PropertyTest_{Guid.NewGuid():N}"))),
        };

        var failedCases = new List<string>();

        foreach (var (user, endpoint, method, request) in testCases)
        {
            using var client = await _fixture.CreateAuthenticatedClientAsync(user.Email, user.Password);
            
            var response = await request(client);
            var statusCode = (int)response.StatusCode;
            
            // Check if response is 2xx (success)
            if (statusCode < 200 || statusCode >= 300)
            {
                failedCases.Add($"User {user.Email} ({user.Role}) calling {method} {endpoint} returned {response.StatusCode} instead of 2xx");
            }
        }

        Assert.True(failedCases.Count == 0, 
            $"Property violation: Authenticated clients did not receive 2xx responses for permitted endpoints:\n{string.Join("\n", failedCases)}");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates a JWT token that has already expired.
    /// </summary>
    private static string GenerateExpiredToken()
    {
        var secretKey = "VyshyvankaDefaultSecretKey123456789012345678901234567890";
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, "test@test.com"),
            new(ClaimTypes.Role, "Editor"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Token expired 1 hour ago
        var token = new JwtSecurityToken(
            issuer: "Vyshyvanka",
            audience: "Vyshyvanka",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(-1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Generates a JWT token signed with a different secret key.
    /// </summary>
    private static string GenerateTokenWithInvalidSignature()
    {
        // Use a different secret key than the one configured in the test server
        var differentSecretKey = "DifferentSecretKey123456789012345678901234567890123";
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(differentSecretKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, "test@test.com"),
            new(ClaimTypes.Role, "Editor"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "Vyshyvanka",
            audience: "Vyshyvanka",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    #endregion
}
