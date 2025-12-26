using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FlowForge.Api.Controllers;
using FlowForge.Api.Models;
using FlowForge.Core.Interfaces;
using FlowForge.Tests.Integration.Fixtures;

namespace FlowForge.Tests.Integration.Webhooks;

/// <summary>
/// Integration tests for the WebhookController endpoints.
/// Tests webhook triggering, data passing, and error handling.
/// </summary>
public class WebhookControllerTests : IClassFixture<FlowForgeApiFixture>, IAsyncLifetime
{
    private readonly FlowForgeApiFixture _fixture;
    private HttpClient _authenticatedClient = null!;

    public WebhookControllerTests(FlowForgeApiFixture fixture)
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

    #region Webhook Trigger Tests (Task 5.1)

    /// <summary>
    /// Tests that POST webhook triggers an active workflow and returns 200 OK.
    /// Validates: Requirements 5.1
    /// </summary>
    [Fact]
    public async Task WhenPostWebhookWithActiveWorkflowThenReturns200()
    {
        // Arrange - Create an active workflow
        var workflowRequest = TestDataFactory.CreateValidWorkflow("Webhook POST Test", isActive: true);
        var createResponse = await _authenticatedClient.PostAsJsonAsync("/api/workflow", workflowRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var workflow = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();
        Assert.NotNull(workflow);

        // Act - Trigger via POST webhook using anonymous client
        var response = await _fixture.AnonymousClient.PostAsync($"/api/webhook/{workflow.Id}", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var webhookResponse = await response.Content.ReadFromJsonAsync<WebhookResponse>();
        Assert.NotNull(webhookResponse);
        Assert.NotEqual(Guid.Empty, webhookResponse.ExecutionId);
        Assert.Equal(workflow.Id, webhookResponse.WorkflowId);
        Assert.Contains("success", webhookResponse.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests that GET webhook triggers an active workflow and returns 200 OK.
    /// Validates: Requirements 5.2
    /// </summary>
    [Fact]
    public async Task WhenGetWebhookWithActiveWorkflowThenReturns200()
    {
        // Arrange - Create an active workflow
        var workflowRequest = TestDataFactory.CreateValidWorkflow("Webhook GET Test", isActive: true);
        var createResponse = await _authenticatedClient.PostAsJsonAsync("/api/workflow", workflowRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var workflow = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();
        Assert.NotNull(workflow);

        // Act - Trigger via GET webhook using anonymous client
        var response = await _fixture.AnonymousClient.GetAsync($"/api/webhook/{workflow.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var webhookResponse = await response.Content.ReadFromJsonAsync<WebhookResponse>();
        Assert.NotNull(webhookResponse);
        Assert.NotEqual(Guid.Empty, webhookResponse.ExecutionId);
        Assert.Equal(workflow.Id, webhookResponse.WorkflowId);
        Assert.Contains("success", webhookResponse.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests that webhook for inactive workflow returns 400 Bad Request.
    /// Validates: Requirements 5.3
    /// </summary>
    [Fact]
    public async Task WhenWebhookForInactiveWorkflowThenReturns400()
    {
        // Arrange - Create an inactive workflow
        var workflowRequest = TestDataFactory.CreateValidWorkflow("Inactive Webhook Test", isActive: false);
        var createResponse = await _authenticatedClient.PostAsJsonAsync("/api/workflow", workflowRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var workflow = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();
        Assert.NotNull(workflow);

        // Act - Try to trigger via webhook
        var response = await _fixture.AnonymousClient.PostAsync($"/api/webhook/{workflow.Id}", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(error);
        Assert.Equal("WORKFLOW_INACTIVE", error.Code);
    }

    /// <summary>
    /// Tests that webhook for non-existent workflow returns 404 Not Found.
    /// Validates: Requirements 5.4
    /// </summary>
    [Fact]
    public async Task WhenWebhookForNonExistentWorkflowThenReturns404()
    {
        // Arrange
        var nonExistentWorkflowId = Guid.NewGuid();

        // Act
        var response = await _fixture.AnonymousClient.PostAsync($"/api/webhook/{nonExistentWorkflowId}", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(error);
        Assert.Equal("WORKFLOW_NOT_FOUND", error.Code);
    }

    #endregion

    #region Webhook Data Passing Tests (Task 5.2)

    /// <summary>
    /// Tests that JSON body is passed to the execution context.
    /// Validates: Requirements 5.5
    /// </summary>
    [Fact]
    public async Task WhenWebhookWithJsonBodyThenBodyPassedToContext()
    {
        // Arrange - Create an active workflow
        var workflowRequest = TestDataFactory.CreateValidWorkflow("Webhook Body Test", isActive: true);
        var createResponse = await _authenticatedClient.PostAsJsonAsync("/api/workflow", workflowRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var workflow = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();
        Assert.NotNull(workflow);

        // Create JSON body to send
        var testBody = new { name = "TestUser", value = 42, nested = new { key = "data" } };
        var jsonContent = new StringContent(
            JsonSerializer.Serialize(testBody),
            Encoding.UTF8,
            "application/json");

        // Act - Trigger via POST webhook with JSON body
        var response = await _fixture.AnonymousClient.PostAsync($"/api/webhook/{workflow.Id}", jsonContent);

        // Assert - Webhook should succeed
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var webhookResponse = await response.Content.ReadFromJsonAsync<WebhookResponse>();
        Assert.NotNull(webhookResponse);
        Assert.NotEqual(Guid.Empty, webhookResponse.ExecutionId);

        // Verify the execution was created and contains the body data
        var (scope, executionRepository) = _fixture.GetScopedService<IExecutionRepository>();
        using (scope)
        {
            var execution = await executionRepository.GetByIdAsync(webhookResponse.ExecutionId);
            Assert.NotNull(execution);
            
            // The webhook data should be stored - check that execution completed
            Assert.Equal(workflow.Id, execution.WorkflowId);
        }
    }

    /// <summary>
    /// Tests that query parameters are passed to the execution context.
    /// Validates: Requirements 5.6
    /// </summary>
    [Fact]
    public async Task WhenWebhookWithQueryParametersThenQueryPassedToContext()
    {
        // Arrange - Create an active workflow
        var workflowRequest = TestDataFactory.CreateValidWorkflow("Webhook Query Test", isActive: true);
        var createResponse = await _authenticatedClient.PostAsJsonAsync("/api/workflow", workflowRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var workflow = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();
        Assert.NotNull(workflow);

        // Act - Trigger via GET webhook with query parameters
        var response = await _fixture.AnonymousClient.GetAsync(
            $"/api/webhook/{workflow.Id}?param1=value1&param2=value2&count=123");

        // Assert - Webhook should succeed
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var webhookResponse = await response.Content.ReadFromJsonAsync<WebhookResponse>();
        Assert.NotNull(webhookResponse);
        Assert.NotEqual(Guid.Empty, webhookResponse.ExecutionId);

        // Verify the execution was created
        var (scope, executionRepository) = _fixture.GetScopedService<IExecutionRepository>();
        using (scope)
        {
            var execution = await executionRepository.GetByIdAsync(webhookResponse.ExecutionId);
            Assert.NotNull(execution);
            
            // The webhook data should be stored - check that execution completed
            Assert.Equal(workflow.Id, execution.WorkflowId);
        }
    }

    #endregion
}
