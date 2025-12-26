using System.Net;
using System.Net.Http.Json;
using CsCheck;
using FlowForge.Api.Models;
using FlowForge.Tests.Integration.Fixtures;

namespace FlowForge.Tests.Integration.Workflows;

/// <summary>
/// Integration tests for the WorkflowController endpoints.
/// Tests CRUD operations, pagination, and search functionality.
/// </summary>
public class WorkflowControllerTests : IClassFixture<FlowForgeApiFixture>, IAsyncLifetime
{
    private readonly FlowForgeApiFixture _fixture;
    private HttpClient _authenticatedClient = null!;

    public WorkflowControllerTests(FlowForgeApiFixture fixture)
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

    #region Create Workflow Tests (Task 3.1)

    [Fact]
    public async Task WhenValidWorkflowThenReturns201WithDetails()
    {
        // Arrange
        var request = TestDataFactory.CreateValidWorkflow("Test Create Workflow");

        // Act
        var response = await _authenticatedClient.PostAsJsonAsync("/api/workflow", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var workflow = await response.Content.ReadFromJsonAsync<WorkflowResponse>();
        Assert.NotNull(workflow);
        Assert.NotEqual(Guid.Empty, workflow.Id);
        Assert.Equal(request.Name, workflow.Name);
        Assert.Equal(request.Description, workflow.Description);
        Assert.Equal(request.IsActive, workflow.IsActive);
        Assert.Equal(1, workflow.Version);
        Assert.NotEmpty(workflow.Nodes);
        Assert.True(workflow.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task WhenInvalidWorkflowThenReturns400WithValidationErrors()
    {
        // Arrange - workflow without trigger node
        var request = TestDataFactory.CreateInvalidWorkflow();

        // Act
        var response = await _authenticatedClient.PostAsJsonAsync("/api/workflow", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(error);
        Assert.Equal("WORKFLOW_VALIDATION_FAILED", error.Code);
        Assert.NotNull(error.Details);
    }

    #endregion

    #region Get Workflow Tests (Task 3.2)

    [Fact]
    public async Task WhenGetByValidIdThenReturns200WithWorkflow()
    {
        // Arrange - Create a workflow first
        var createRequest = TestDataFactory.CreateValidWorkflow("Test Get Workflow");
        var createResponse = await _authenticatedClient.PostAsJsonAsync("/api/workflow", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();
        Assert.NotNull(created);

        // Act
        var response = await _authenticatedClient.GetAsync($"/api/workflow/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var workflow = await response.Content.ReadFromJsonAsync<WorkflowResponse>();
        Assert.NotNull(workflow);
        Assert.Equal(created.Id, workflow.Id);
        Assert.Equal(created.Name, workflow.Name);
        Assert.Equal(created.Version, workflow.Version);
    }

    [Fact]
    public async Task WhenGetByNonExistentIdThenReturns404()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _authenticatedClient.GetAsync($"/api/workflow/{nonExistentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(error);
        Assert.Equal("WORKFLOW_NOT_FOUND", error.Code);
    }

    #endregion


    #region Update Workflow Tests (Task 3.3)

    [Fact]
    public async Task WhenUpdateWithCorrectVersionThenReturns200WithIncrementedVersion()
    {
        // Arrange - Create a workflow first
        var createRequest = TestDataFactory.CreateValidWorkflow("Test Update Workflow");
        var createResponse = await _authenticatedClient.PostAsJsonAsync("/api/workflow", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();
        Assert.NotNull(created);

        var updateRequest = TestDataFactory.CreateUpdateRequest(created, "Updated Workflow Name");

        // Act
        var response = await _authenticatedClient.PutAsJsonAsync($"/api/workflow/{created.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<WorkflowResponse>();
        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Updated Workflow Name", updated.Name);
        Assert.Equal(created.Version + 1, updated.Version);
    }

    [Fact]
    public async Task WhenUpdateWithWrongVersionThenReturns409Conflict()
    {
        // Arrange - Create a workflow first
        var createRequest = TestDataFactory.CreateValidWorkflow("Test Version Conflict");
        var createResponse = await _authenticatedClient.PostAsJsonAsync("/api/workflow", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();
        Assert.NotNull(created);

        // Create update request with wrong version
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
        Assert.Equal("WORKFLOW_VERSION_CONFLICT", error.Code);
    }

    #endregion

    #region Delete Workflow Tests (Task 3.4)

    [Fact]
    public async Task WhenDeleteExistingWorkflowThenReturns204()
    {
        // Arrange - Create a workflow first
        var createRequest = TestDataFactory.CreateValidWorkflow("Test Delete Workflow");
        var createResponse = await _authenticatedClient.PostAsJsonAsync("/api/workflow", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();
        Assert.NotNull(created);

        // Act
        var response = await _authenticatedClient.DeleteAsync($"/api/workflow/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify workflow is deleted
        var getResponse = await _authenticatedClient.GetAsync($"/api/workflow/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task WhenDeleteNonExistentWorkflowThenReturns404()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _authenticatedClient.DeleteAsync($"/api/workflow/{nonExistentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(error);
        Assert.Equal("WORKFLOW_NOT_FOUND", error.Code);
    }

    #endregion

    #region Pagination Property Tests (Task 3.5)

    /// <summary>
    /// Property 3: Pagination Bounds Respected
    /// For any valid pagination parameters (skip >= 0, take > 0), 
    /// the GET /api/workflow endpoint SHALL return at most `take` items.
    /// Validates: Requirements 3.9
    /// </summary>
    [Fact]
    public async Task WhenPaginationParametersThenBoundsRespected()
    {
        // Feature: api-integration-tests, Property 3: Pagination Bounds Respected
        // Validates: Requirements 3.9
        
        // Create some workflows to ensure we have data to paginate
        for (var i = 0; i < 5; i++)
        {
            var workflow = TestDataFactory.CreateValidWorkflow($"PaginationTest_{i}_{Guid.NewGuid():N}");
            await _authenticatedClient.PostAsJsonAsync("/api/workflow", workflow);
        }

        // Generate test cases using CsCheck - generate 100 (skip, take) pairs
        var skipGen = Gen.Int[0, 10];
        var takeGen = Gen.Int[1, 20];
        var testCases = new List<(int skip, int take)>();
        
        skipGen.Sample(skip =>
        {
            takeGen.Sample(take =>
            {
                testCases.Add((skip, take));
            }, iter: 10);
        }, iter: 10);

        foreach (var (skip, take) in testCases)
        {
            // Act
            var response = await _authenticatedClient.GetAsync($"/api/workflow?skip={skip}&take={take}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<PagedResponse<WorkflowResponse>>();
            Assert.NotNull(result);
            Assert.True(result.Items.Count <= take, 
                $"Expected at most {take} items but got {result.Items.Count}");
            Assert.Equal(skip, result.Skip);
            Assert.Equal(take, result.Take);
        }
    }

    #endregion

    #region Search Property Tests (Task 3.6)

    /// <summary>
    /// Property 4: Search Results Match Query
    /// For any search query string, all workflows returned by GET /api/workflow?search={query} 
    /// SHALL contain the query string in their name or description.
    /// Validates: Requirements 3.10
    /// </summary>
    [Fact]
    public async Task WhenSearchQueryThenResultsMatchQuery()
    {
        // Feature: api-integration-tests, Property 4: Search Results Match Query
        // Validates: Requirements 3.10
        
        // Arrange - Create workflows with known names for searching
        var uniquePrefix = $"SearchTest_{Guid.NewGuid():N}";
        var workflow1 = TestDataFactory.CreateValidWorkflow($"{uniquePrefix}_Alpha");
        var workflow2 = TestDataFactory.CreateValidWorkflow($"{uniquePrefix}_Beta");
        var workflow3 = TestDataFactory.CreateValidWorkflow("OtherWorkflow");

        await _authenticatedClient.PostAsJsonAsync("/api/workflow", workflow1);
        await _authenticatedClient.PostAsJsonAsync("/api/workflow", workflow2);
        await _authenticatedClient.PostAsJsonAsync("/api/workflow", workflow3);

        // Act - Search for the unique prefix
        var response = await _authenticatedClient.GetAsync($"/api/workflow?search={uniquePrefix}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PagedResponse<WorkflowResponse>>();
        Assert.NotNull(result);
        
        // All returned items should contain the search query in name or description
        foreach (var item in result.Items)
        {
            var matchesName = item.Name.Contains(uniquePrefix, StringComparison.OrdinalIgnoreCase);
            var matchesDescription = item.Description?.Contains(uniquePrefix, StringComparison.OrdinalIgnoreCase) ?? false;
            Assert.True(matchesName || matchesDescription,
                $"Workflow '{item.Name}' does not contain search query '{uniquePrefix}'");
        }
    }

    #endregion
}
