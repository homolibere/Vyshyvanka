using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Vyshyvanka.Api.Models;
using Vyshyvanka.Tests.Integration.Fixtures;

namespace Vyshyvanka.Tests.Integration;

public class WorkflowApiTests : IClassFixture<VyshyvankaWebApplicationFactory>
{
    private readonly HttpClient _client;

    public WorkflowApiTests(VyshyvankaWebApplicationFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
    }

    private static CreateWorkflowRequest CreateValidRequest(string name = "Test Workflow") => new()
    {
        Name = name,
        Description = "A test workflow",
        IsActive = true,
        Nodes =
        [
            new WorkflowNodeDto
            {
                Id = "trigger-1",
                Type = "manual-trigger",
                Name = "Start",
                Position = new PositionDto(0, 0)
            },
            new WorkflowNodeDto
            {
                Id = "action-1",
                Type = "http-request",
                Name = "Call API",
                Position = new PositionDto(200, 0)
            }
        ],
        Connections =
        [
            new ConnectionDto
            {
                SourceNodeId = "trigger-1",
                SourcePort = "output",
                TargetNodeId = "action-1",
                TargetPort = "input"
            }
        ]
    };

    // --- Create ---

    [Fact]
    public async Task WhenCreatingValidWorkflowThenReturns201()
    {
        var request = CreateValidRequest();

        var response = await _client.PostAsJsonAsync("/api/workflow", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var workflow = await response.Content.ReadFromJsonAsync<WorkflowResponse>();
        workflow.Should().NotBeNull();
        workflow!.Name.Should().Be("Test Workflow");
        workflow.Id.Should().NotBeEmpty();
        workflow.Version.Should().Be(1);
    }

    [Fact]
    public async Task WhenCreatingWorkflowWithoutTriggerThenReturns400()
    {
        var request = new CreateWorkflowRequest
        {
            Name = "No Trigger",
            Nodes =
            [
                new WorkflowNodeDto
                {
                    Id = "action-1",
                    Type = "http-request",
                    Name = "Action",
                    Position = new PositionDto(0, 0)
                }
            ]
        };

        var response = await _client.PostAsJsonAsync("/api/workflow", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("WORKFLOW_VALIDATION_FAILED");
    }

    // --- Get by ID ---

    [Fact]
    public async Task WhenGettingExistingWorkflowThenReturns200()
    {
        // Create first
        var createResponse = await _client.PostAsJsonAsync("/api/workflow", CreateValidRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();

        // Get
        var response = await _client.GetAsync($"/api/workflow/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var workflow = await response.Content.ReadFromJsonAsync<WorkflowResponse>();
        workflow!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task WhenGettingNonexistentWorkflowThenReturns404()
    {
        var response = await _client.GetAsync($"/api/workflow/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Get all ---

    [Fact]
    public async Task WhenGettingAllWorkflowsThenReturnsPaginatedList()
    {
        await _client.PostAsJsonAsync("/api/workflow", CreateValidRequest("WF 1"));
        await _client.PostAsJsonAsync("/api/workflow", CreateValidRequest("WF 2"));

        var response = await _client.GetAsync("/api/workflow?skip=0&take=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<WorkflowResponse>>();
        paged.Should().NotBeNull();
        paged!.Items.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    // --- Update ---

    [Fact]
    public async Task WhenUpdatingWorkflowThenReturns200WithNewVersion()
    {
        // Create
        var createResponse = await _client.PostAsJsonAsync("/api/workflow", CreateValidRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();

        // Update
        var updateRequest = new UpdateWorkflowRequest
        {
            Name = "Updated Workflow",
            IsActive = false,
            Version = created!.Version,
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

        var response = await _client.PutAsJsonAsync($"/api/workflow/{created.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<WorkflowResponse>();
        updated!.Name.Should().Be("Updated Workflow");
        updated.Version.Should().Be(created.Version + 1);
    }

    [Fact]
    public async Task WhenUpdatingWithWrongVersionThenReturns409()
    {
        // Create
        var createResponse = await _client.PostAsJsonAsync("/api/workflow", CreateValidRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();

        // Update with wrong version
        var updateRequest = new UpdateWorkflowRequest
        {
            Name = "Updated",
            Version = 999,
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

        var response = await _client.PutAsJsonAsync($"/api/workflow/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // --- Delete ---

    [Fact]
    public async Task WhenDeletingWorkflowThenReturns204()
    {
        // Create
        var createResponse = await _client.PostAsJsonAsync("/api/workflow", CreateValidRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();

        // Delete
        var response = await _client.DeleteAsync($"/api/workflow/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deleted
        var getResponse = await _client.GetAsync($"/api/workflow/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WhenDeletingNonexistentWorkflowThenReturns404()
    {
        var response = await _client.DeleteAsync($"/api/workflow/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Active workflows ---

    [Fact]
    public async Task WhenGettingActiveWorkflowsThenReturnsOnlyActive()
    {
        await _client.PostAsJsonAsync("/api/workflow", CreateValidRequest("Active") with { IsActive = true });

        var response = await _client.GetAsync("/api/workflow/active");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<WorkflowResponse>>();
        paged.Should().NotBeNull();
        paged!.Items.Should().OnlyContain(w => w.IsActive);
    }
}
