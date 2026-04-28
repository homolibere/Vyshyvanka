using System.Net;
using System.Net.Http.Json;
using CsCheck;
using Vyshyvanka.Api.Models;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Tests.Integration.Fixtures;

namespace Vyshyvanka.Tests.Integration.Executions;

/// <summary>
/// Integration tests for the ExecutionController endpoints.
/// Tests execution triggering, retrieval, and cancellation.
/// </summary>
public class ExecutionControllerTests : IClassFixture<VyshyvankaApiFixture>, IAsyncLifetime
{
    private readonly VyshyvankaApiFixture _fixture;
    private HttpClient _authenticatedClient = null!;

    public ExecutionControllerTests(VyshyvankaApiFixture fixture)
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

    #region Trigger Execution Tests (Task 4.1)

    /// <summary>
    /// Tests that triggering execution on a valid active workflow returns 202 Accepted with execution details.
    /// Validates: Requirements 4.1
    /// </summary>
    [Fact]
    public async Task WhenValidExecutionRequestThenReturns202WithDetails()
    {
        // Arrange - Create an active workflow first
        var workflowRequest = TestDataFactory.CreateValidWorkflow("Execution Test Workflow", isActive: true);
        var createResponse = await _authenticatedClient.PostAsJsonAsync("/api/workflow", workflowRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        
        var workflow = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();
        Assert.NotNull(workflow);

        var executionRequest = TestDataFactory.CreateExecutionRequest(workflow.Id);

        // Act
        var response = await _authenticatedClient.PostAsJsonAsync("/api/execution", executionRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var execution = await response.Content.ReadFromJsonAsync<ExecutionResponse>();
        Assert.NotNull(execution);
        Assert.NotEqual(Guid.Empty, execution.Id);
        Assert.Equal(workflow.Id, execution.WorkflowId);
        Assert.Equal(workflow.Version, execution.WorkflowVersion);
        Assert.True(execution.Status == ExecutionStatus.Completed || execution.Status == ExecutionStatus.Running);
        Assert.True(execution.StartedAt <= DateTime.UtcNow);
    }

    /// <summary>
    /// Tests that triggering execution on an inactive workflow returns 400 Bad Request.
    /// Validates: Requirements 4.2
    /// </summary>
    [Fact]
    public async Task WhenTriggerInactiveWorkflowThenReturns400()
    {
        // Arrange - Create an inactive workflow
        var workflowRequest = TestDataFactory.CreateValidWorkflow("Inactive Workflow", isActive: false);
        var createResponse = await _authenticatedClient.PostAsJsonAsync("/api/workflow", workflowRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        
        var workflow = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();
        Assert.NotNull(workflow);

        var executionRequest = TestDataFactory.CreateExecutionRequest(workflow.Id);

        // Act
        var response = await _authenticatedClient.PostAsJsonAsync("/api/execution", executionRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(error);
        Assert.Equal("WORKFLOW_INACTIVE", error.Code);
    }

    /// <summary>
    /// Tests that triggering execution on a non-existent workflow returns 404 Not Found.
    /// Validates: Requirements 4.3
    /// </summary>
    [Fact]
    public async Task WhenTriggerNonExistentWorkflowThenReturns404()
    {
        // Arrange
        var nonExistentWorkflowId = Guid.NewGuid();
        var executionRequest = TestDataFactory.CreateExecutionRequest(nonExistentWorkflowId);

        // Act
        var response = await _authenticatedClient.PostAsJsonAsync("/api/execution", executionRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(error);
        Assert.Equal("WORKFLOW_NOT_FOUND", error.Code);
    }

    #endregion

    #region Execution Retrieval Tests (Task 4.2)

    /// <summary>
    /// Tests that getting an execution by valid ID returns 200 OK with execution details.
    /// Validates: Requirements 4.4
    /// </summary>
    [Fact]
    public async Task WhenGetByValidIdThenReturns200WithExecution()
    {
        // Arrange - Create a workflow and trigger an execution
        var workflowRequest = TestDataFactory.CreateValidWorkflow("Retrieval Test Workflow", isActive: true);
        var createResponse = await _authenticatedClient.PostAsJsonAsync("/api/workflow", workflowRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        
        var workflow = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();
        Assert.NotNull(workflow);

        var executionRequest = TestDataFactory.CreateExecutionRequest(workflow.Id);
        var triggerResponse = await _authenticatedClient.PostAsJsonAsync("/api/execution", executionRequest);
        Assert.Equal(HttpStatusCode.Accepted, triggerResponse.StatusCode);
        
        var triggeredExecution = await triggerResponse.Content.ReadFromJsonAsync<ExecutionResponse>();
        Assert.NotNull(triggeredExecution);

        // Act
        var response = await _authenticatedClient.GetAsync($"/api/execution/{triggeredExecution.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var execution = await response.Content.ReadFromJsonAsync<ExecutionResponse>();
        Assert.NotNull(execution);
        Assert.Equal(triggeredExecution.Id, execution.Id);
        Assert.Equal(workflow.Id, execution.WorkflowId);
        Assert.Equal(workflow.Version, execution.WorkflowVersion);
    }

    /// <summary>
    /// Tests that getting an execution by non-existent ID returns 404 Not Found.
    /// Validates: Requirements 4.5
    /// </summary>
    [Fact]
    public async Task WhenGetByNonExistentIdThenReturns404()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _authenticatedClient.GetAsync($"/api/execution/{nonExistentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(error);
        Assert.Equal("EXECUTION_NOT_FOUND", error.Code);
    }

    #endregion

    #region Execution Cancellation Tests (Task 4.3)

    /// <summary>
    /// Tests that cancelling a running execution returns 202 Accepted.
    /// Validates: Requirements 4.8
    /// </summary>
    [Fact]
    public async Task WhenCancelRunningExecutionThenReturns202()
    {
        // Arrange - Create a workflow first
        var workflowRequest = TestDataFactory.CreateValidWorkflow("Cancel Test Workflow", isActive: true);
        var createResponse = await _authenticatedClient.PostAsJsonAsync("/api/workflow", workflowRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        
        var workflow = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();
        Assert.NotNull(workflow);

        // Create an execution record directly with Running status
        var (scope, executionRepository) = _fixture.GetScopedService<IExecutionRepository>();
        using (scope)
        {
            var runningExecution = new Execution
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflow.Id,
                WorkflowVersion = workflow.Version,
                Status = ExecutionStatus.Running,
                Mode = ExecutionMode.Api,
                StartedAt = DateTime.UtcNow
            };
            await executionRepository.CreateAsync(runningExecution);

            // Act
            var response = await _authenticatedClient.PostAsync($"/api/execution/{runningExecution.Id}/cancel", null);

            // Assert
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }
    }

    /// <summary>
    /// Tests that cancelling a completed execution returns 400 Bad Request.
    /// Validates: Requirements 4.9
    /// </summary>
    [Fact]
    public async Task WhenCancelCompletedExecutionThenReturns400()
    {
        // Arrange - Create a workflow and trigger an execution (which completes immediately)
        var workflowRequest = TestDataFactory.CreateValidWorkflow("Cancel Completed Test", isActive: true);
        var createResponse = await _authenticatedClient.PostAsJsonAsync("/api/workflow", workflowRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        
        var workflow = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();
        Assert.NotNull(workflow);

        var executionRequest = TestDataFactory.CreateExecutionRequest(workflow.Id);
        var triggerResponse = await _authenticatedClient.PostAsJsonAsync("/api/execution", executionRequest);
        Assert.Equal(HttpStatusCode.Accepted, triggerResponse.StatusCode);
        
        var execution = await triggerResponse.Content.ReadFromJsonAsync<ExecutionResponse>();
        Assert.NotNull(execution);
        Assert.Equal(ExecutionStatus.Completed, execution.Status);

        // Act - Try to cancel the completed execution
        var response = await _authenticatedClient.PostAsync($"/api/execution/{execution.Id}/cancel", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(error);
        Assert.Equal("EXECUTION_NOT_CANCELLABLE", error.Code);
    }

    #endregion

    #region Execution Workflow Filter Property Tests (Task 4.4)

    /// <summary>
    /// Property 5: Execution Workflow Filter Consistency
    /// For any workflow ID, all executions returned by GET /api/execution/workflow/{workflowId} 
    /// SHALL have their WorkflowId property equal to the requested workflow ID.
    /// Validates: Requirements 4.7
    /// </summary>
    [Fact]
    public async Task WhenFilterByWorkflowIdThenAllExecutionsMatchWorkflowId()
    {
        // Feature: api-integration-tests, Property 5: Execution Workflow Filter Consistency
        // Validates: Requirements 4.7
        
        // Arrange - Create multiple workflows with executions
        var workflowIds = new List<Guid>();
        const int workflowCount = 3;
        const int executionsPerWorkflow = 2;

        for (var i = 0; i < workflowCount; i++)
        {
            var workflowRequest = TestDataFactory.CreateValidWorkflow($"FilterTest_{i}_{Guid.NewGuid():N}", isActive: true);
            var createResponse = await _authenticatedClient.PostAsJsonAsync("/api/workflow", workflowRequest);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            
            var workflow = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();
            Assert.NotNull(workflow);
            workflowIds.Add(workflow.Id);

            // Create multiple executions for each workflow
            for (var j = 0; j < executionsPerWorkflow; j++)
            {
                var executionRequest = TestDataFactory.CreateExecutionRequest(workflow.Id);
                var execResponse = await _authenticatedClient.PostAsJsonAsync("/api/execution", executionRequest);
                Assert.Equal(HttpStatusCode.Accepted, execResponse.StatusCode);
            }
        }

        // Generate test cases using CsCheck - select random workflow indices
        var workflowIndexGen = Gen.Int[0, workflowCount - 1];
        var testCases = new List<int>();
        
        workflowIndexGen.Sample(index =>
        {
            testCases.Add(index);
        }, iter: 100);

        // Act & Assert - For each generated workflow index, verify filter consistency
        foreach (var workflowIndex in testCases)
        {
            var workflowId = workflowIds[workflowIndex];
            var response = await _authenticatedClient.GetAsync($"/api/execution/workflow/{workflowId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<PagedResponse<ExecutionSummaryResponse>>();
            Assert.NotNull(result);

            // Property: All returned executions must have WorkflowId equal to the requested workflow ID
            foreach (var execution in result.Items)
            {
                Assert.True(workflowId == execution.WorkflowId,
                    $"Execution {execution.Id} has WorkflowId {execution.WorkflowId} but was returned for workflow {workflowId}");
            }
        }
    }

    #endregion
}
