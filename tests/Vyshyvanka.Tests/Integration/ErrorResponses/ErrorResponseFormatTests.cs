using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CsCheck;
using Vyshyvanka.Api.Models;
using Vyshyvanka.Tests.Integration.Fixtures;

namespace Vyshyvanka.Tests.Integration.ErrorResponses;

/// <summary>
/// Integration tests for error response format consistency.
/// Tests that all 4xx responses follow the standard ApiError format.
/// </summary>
public class ErrorResponseFormatTests : IClassFixture<VyshyvankaApiFixture>, IAsyncLifetime
{
    private readonly VyshyvankaApiFixture _fixture;
    private HttpClient _authenticatedClient = null!;

    public ErrorResponseFormatTests(VyshyvankaApiFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _authenticatedClient = await _fixture.CreateAuthenticatedClientAsync();
    }

    public Task DisposeAsync()
    {
        _authenticatedClient.Dispose();
        return Task.CompletedTask;
    }

    #region Property Test: Error Response Format Consistency (Task 8.1)

    /// <summary>
    /// Property 6: Error Response Format Consistency
    /// For any API endpoint that returns a 4xx status code (400, 401, 403, 404, 409),
    /// the response body SHALL contain a JSON object with `code` (string) and `message` (string) properties.
    /// Validates: Requirements 8.1, 8.2, 8.3
    /// </summary>
    [Fact]
    public async Task WhenEndpointReturns4XxThenResponseContainsCodeAndMessage()
    {
        // Feature: api-integration-tests, Property 6: Error Response Format Consistency
        // Validates: Requirements 8.1, 8.2, 8.3

        // Define error scenarios that produce 4xx responses
        var errorScenarios = new List<ErrorScenario>
        {
            // 404 Not Found scenarios
            new("GET", "/api/workflow/{id}",
                client => client.GetAsync($"/api/workflow/{Guid.NewGuid()}")),
            new("GET", "/api/execution/{id}",
                client => client.GetAsync($"/api/execution/{Guid.NewGuid()}")),
            new("DELETE", "/api/workflow/{id}",
                client => client.DeleteAsync($"/api/workflow/{Guid.NewGuid()}")),
            
            // 400 Bad Request scenarios - workflow validation
            new("POST", "/api/workflow (invalid)",
                client => client.PostAsJsonAsync("/api/workflow", TestDataFactory.CreateInvalidWorkflow())),
        };

        // Generate random GUIDs for additional 404 scenarios using CsCheck
        var guidGen = Gen.Guid;
        guidGen.Sample(guid =>
        {
            errorScenarios.Add(new ErrorScenario(
                "GET", $"/api/workflow/{guid}",
                client => client.GetAsync($"/api/workflow/{guid}")));
        }, iter: 10);

        // Test each error scenario
        foreach (var scenario in errorScenarios)
        {
            // Act
            var response = await scenario.ExecuteAsync(_authenticatedClient);

            // Assert - verify it's a 4xx response
            var statusCode = (int)response.StatusCode;
            Assert.True(statusCode >= 400 && statusCode < 500,
                $"Expected 4xx status code for {scenario.Method} {scenario.Endpoint}, got {statusCode}");

            // Assert - verify response body contains code and message
            var content = await response.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrWhiteSpace(content),
                $"Response body should not be empty for {scenario.Method} {scenario.Endpoint}");

            var jsonDoc = JsonDocument.Parse(content);
            var root = jsonDoc.RootElement;

            // Check for 'code' property (case-insensitive due to camelCase)
            var hasCode = root.TryGetProperty("code", out var codeElement) ||
                          root.TryGetProperty("Code", out codeElement);
            Assert.True(hasCode,
                $"Response for {scenario.Method} {scenario.Endpoint} should contain 'code' property. Response: {content}");
            Assert.Equal(JsonValueKind.String, codeElement.ValueKind);
            Assert.False(string.IsNullOrWhiteSpace(codeElement.GetString()),
                $"'code' property should not be empty for {scenario.Method} {scenario.Endpoint}");

            // Check for 'message' property (case-insensitive due to camelCase)
            var hasMessage = root.TryGetProperty("message", out var messageElement) ||
                             root.TryGetProperty("Message", out messageElement);
            Assert.True(hasMessage,
                $"Response for {scenario.Method} {scenario.Endpoint} should contain 'message' property. Response: {content}");
            Assert.Equal(JsonValueKind.String, messageElement.ValueKind);
            Assert.False(string.IsNullOrWhiteSpace(messageElement.GetString()),
                $"'message' property should not be empty for {scenario.Method} {scenario.Endpoint}");
        }
    }

    /// <summary>
    /// Tests 409 Conflict error response format.
    /// </summary>
    [Fact]
    public async Task When409ConflictThenResponseContainsCodeAndMessage()
    {
        // Feature: api-integration-tests, Property 6: Error Response Format Consistency
        // Validates: Requirements 8.3

        // Arrange - Create a workflow first
        var createRequest = TestDataFactory.CreateValidWorkflow("ConflictTest");
        var createResponse = await _authenticatedClient.PostAsJsonAsync("/api/workflow", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();
        Assert.NotNull(created);

        // Create update request with wrong version to trigger 409
        var updateRequest = new UpdateWorkflowRequest
        {
            Name = "Updated Name",
            Description = created.Description,
            IsActive = created.IsActive,
            Nodes = created.Nodes,
            Connections = created.Connections,
            Settings = created.Settings,
            Tags = created.Tags,
            Version = created.Version + 100 // Wrong version
        };

        // Act
        var response = await _authenticatedClient.PutAsJsonAsync($"/api/workflow/{created.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error.Code));
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    #endregion

    #region Validation Error Format Test (Task 8.2)

    /// <summary>
    /// Tests that validation errors include field-level details.
    /// Validates: Requirements 8.4
    /// </summary>
    [Fact]
    public async Task WhenValidationFailsThenResponseIncludesFieldLevelDetails()
    {
        // Arrange - Create an invalid workflow (no trigger node)
        var invalidWorkflow = TestDataFactory.CreateInvalidWorkflow();

        // Act
        var response = await _authenticatedClient.PostAsJsonAsync("/api/workflow", invalidWorkflow);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(error);
        Assert.Equal("WORKFLOW_VALIDATION_FAILED", error.Code);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        
        // Verify field-level details are present
        Assert.NotNull(error.Details);
        Assert.NotEmpty(error.Details);
        
        // Each detail entry should have a key (field path) and value (error messages)
        foreach (var detail in error.Details)
        {
            Assert.False(string.IsNullOrWhiteSpace(detail.Key),
                "Detail key (field path) should not be empty");
            Assert.NotNull(detail.Value);
            Assert.NotEmpty(detail.Value);
            Assert.All(detail.Value, msg => Assert.False(string.IsNullOrWhiteSpace(msg),
                "Error message should not be empty"));
        }
    }

    #endregion

    /// <summary>
    /// Represents an error scenario for testing.
    /// </summary>
    private record ErrorScenario(
        string Method,
        string Endpoint,
        Func<HttpClient, Task<HttpResponseMessage>> ExecuteAsync);
}
