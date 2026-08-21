using System.Net;
using System.Net.Http.Json;
using Vyshyvanka.Api.Models;
using Vyshyvanka.Contracts.Executions;
using Vyshyvanka.Contracts.Workflows;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Tests.Integration.Fixtures;

namespace Vyshyvanka.Tests.Integration;

public class ExecutionApiTests : IClassFixture<VyshyvankaWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ExecutionApiTests(VyshyvankaWebApplicationFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
    }

    private async Task<WorkflowResponse> CreateActiveWorkflow()
    {
        var request = new CreateWorkflowRequest
        {
            Name = "Execution Test Workflow",
            Status = WorkflowStatus.Active,
            Nodes =
            [
                new WorkflowNodeDto
                {
                    Id = "trigger-1",
                    Type = "manual-trigger",
                    Name = "Start",
                    Position = new PositionDto(0, 0)
                }
            ]
        };

        var response = await _client.PostAsJsonAsync("/api/workflow", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkflowResponse>())!;
    }

    // --- Trigger execution ---

    [Fact]
    public async Task WhenTriggeringExecutionForNonexistentWorkflowThenReturns404()
    {
        var request = new TriggerExecutionRequest
        {
            WorkflowId = Guid.NewGuid()
        };

        var response = await _client.PostAsJsonAsync("/api/execution", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WhenManuallyTriggeringDraftWorkflowThenAllowed()
    {
        // Draft workflows are NOT armed for automatic triggers, but manual/API execution
        // must still be allowed so a workflow can be developed and tested from the Designer.
        var createRequest = new CreateWorkflowRequest
        {
            Name = "Draft Workflow",
            Status = WorkflowStatus.Draft,
            Nodes =
            [
                new WorkflowNodeDto
                {
                    Id = "trigger-1",
                    Type = "manual-trigger",
                    Name = "Start",
                    Position = new PositionDto(0, 0)
                }
            ]
        };
        var createResponse = await _client.PostAsJsonAsync("/api/workflow", createRequest);
        var workflow = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();

        var request = new TriggerExecutionRequest
        {
            WorkflowId = workflow!.Id,
            Mode = ExecutionMode.Manual
        };

        var response = await _client.PostAsJsonAsync("/api/execution", request);

        // Manual execution of a Draft workflow is permitted (not gated on activation).
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task WhenAutomaticTriggerModeForNonActiveWorkflowThenReturns400()
    {
        // Automatic-trigger modes (Trigger/Scheduled) are only served for Active workflows.
        var createRequest = new CreateWorkflowRequest
        {
            Name = "Draft Workflow (auto-trigger attempt)",
            Status = WorkflowStatus.Draft,
            Nodes =
            [
                new WorkflowNodeDto
                {
                    Id = "trigger-1",
                    Type = "manual-trigger",
                    Name = "Start",
                    Position = new PositionDto(0, 0)
                }
            ]
        };
        var createResponse = await _client.PostAsJsonAsync("/api/workflow", createRequest);
        var workflow = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();

        var request = new TriggerExecutionRequest
        {
            WorkflowId = workflow!.Id,
            Mode = ExecutionMode.Scheduled
        };

        var response = await _client.PostAsJsonAsync("/api/execution", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        error!.Code.Should().Be("WORKFLOW_NOT_ACTIVE");
    }

    // --- Get execution by ID ---

    [Fact]
    public async Task WhenGettingNonexistentExecutionThenReturns404()
    {
        var response = await _client.GetAsync($"/api/execution/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Get execution history ---

    [Fact]
    public async Task WhenGettingExecutionHistoryThenReturnsPaginatedList()
    {
        var response = await _client.GetAsync("/api/execution?skip=0&take=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<ExecutionSummaryResponse>>();
        paged.Should().NotBeNull();
    }

    // --- Get executions by workflow ---

    [Fact]
    public async Task WhenGettingExecutionsByWorkflowThenReturnsList()
    {
        var workflowId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/execution/workflow/{workflowId}?skip=0&take=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- Get executions by status ---

    [Fact]
    public async Task WhenGettingExecutionsByStatusThenReturnsList()
    {
        var response = await _client.GetAsync("/api/execution/status/Completed?skip=0&take=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- Cancel execution ---

    [Fact]
    public async Task WhenCancellingNonexistentExecutionThenReturns404()
    {
        var response = await _client.PostAsync($"/api/execution/{Guid.NewGuid()}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Partial execution with target node ---

    [Fact]
    public async Task WhenTriggeringWithInvalidTargetNodeThenReturns400()
    {
        var workflow = await CreateActiveWorkflow();

        var request = new TriggerExecutionRequest
        {
            WorkflowId = workflow.Id,
            TargetNodeId = "nonexistent-node"
        };

        var response = await _client.PostAsJsonAsync("/api/execution", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        error!.Code.Should().Be("NODE_NOT_FOUND");
    }
}
