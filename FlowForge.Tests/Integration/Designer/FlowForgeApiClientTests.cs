using System.Net;
using System.Text.Json;
using CsCheck;
using FlowForge.Core.Enums;
using FlowForge.Core.Models;
using FlowForge.Designer.Models;
using FlowForge.Designer.Services;
using FlowForge.Tests.Integration.Designer.Generators;

namespace FlowForge.Tests.Integration.Designer;

/// <summary>
/// Integration tests for FlowForgeApiClient.
/// Tests workflow deserialization, node definition deserialization, and error handling.
/// </summary>
public class FlowForgeApiClientTests
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Converts a Workflow to WorkflowResponse format for mocking API responses.
    /// </summary>
    private static WorkflowResponse ToWorkflowResponse(Workflow workflow) => new()
    {
        Id = workflow.Id,
        Name = workflow.Name,
        Description = workflow.Description,
        Version = workflow.Version,
        IsActive = workflow.IsActive,
        Nodes = workflow.Nodes.Select(n => new WorkflowNodeDto
        {
            Id = n.Id,
            Type = n.Type,
            Name = n.Name,
            Configuration = n.Configuration.ValueKind != JsonValueKind.Undefined ? n.Configuration : null,
            Position = new PositionDto(n.Position.X, n.Position.Y),
            CredentialId = n.CredentialId
        }).ToList(),
        Connections = workflow.Connections.Select(c => new ConnectionDto
        {
            SourceNodeId = c.SourceNodeId,
            SourcePort = c.SourcePort,
            TargetNodeId = c.TargetNodeId,
            TargetPort = c.TargetPort
        }).ToList(),
        Settings = workflow.Settings is not null
            ? new WorkflowSettingsDto
            {
                TimeoutSeconds = workflow.Settings.Timeout.HasValue
                    ? (int)workflow.Settings.Timeout.Value.TotalSeconds
                    : null,
                MaxRetries = workflow.Settings.MaxRetries,
                ErrorHandling = workflow.Settings.ErrorHandling
            }
            : null,
        Tags = workflow.Tags,
        CreatedAt = workflow.CreatedAt,
        UpdatedAt = workflow.UpdatedAt,
        CreatedBy = workflow.CreatedBy
    };

    /// <summary>
    /// Wraps workflow responses in a PagedWorkflowResponse for list endpoints.
    /// </summary>
    private static PagedWorkflowResponse ToPagedResponse(IEnumerable<Workflow> workflows)
    {
        var items = workflows.Select(ToWorkflowResponse).ToList();
        return new PagedWorkflowResponse
        {
            Items = items,
            Skip = 0,
            Take = items.Count,
            TotalCount = items.Count
        };
    }

    private static FlowForgeApiClient CreateClient(MockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://test-api/")
        };
        return new FlowForgeApiClient(httpClient);
    }

    #region Workflow Deserialization Tests (Task 13.1)

    /// <summary>
    /// Tests that GetWorkflowsAsync deserializes workflow list correctly.
    /// Validates: Requirements 8.1
    /// </summary>
    [Fact]
    public async Task WhenFetchingWorkflowsThenDeserializesResponseIntoWorkflowObjects()
    {
        // Arrange
        var workflows = new List<Workflow>
        {
            TestFixtures.CreateSimpleWorkflow(),
            TestFixtures.CreateBranchingWorkflow()
        };
        var pagedResponse = ToPagedResponse(workflows);
        var json = JsonSerializer.Serialize(pagedResponse, CamelCaseOptions);

        var handler = new MockHttpMessageHandler()
            .SetupGet("/api/workflow", HttpStatusCode.OK, json);

        var client = CreateClient(handler);

        // Act
        var result = await client.GetWorkflowsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal(workflows[0].Id, result[0].Id);
        Assert.Equal(workflows[0].Name, result[0].Name);
        Assert.Equal(workflows[1].Id, result[1].Id);
        Assert.Equal(workflows[1].Name, result[1].Name);
    }

    /// <summary>
    /// Tests that GetWorkflowAsync deserializes single workflow correctly.
    /// Validates: Requirements 8.1
    /// </summary>
    [Fact]
    public async Task WhenFetchingSingleWorkflowThenDeserializesResponseIntoWorkflowObject()
    {
        // Arrange
        var workflow = TestFixtures.CreateSimpleWorkflow();
        var response = ToWorkflowResponse(workflow);
        var json = JsonSerializer.Serialize(response, CamelCaseOptions);

        var handler = new MockHttpMessageHandler()
            .SetupGet($"/api/workflow/{workflow.Id}", HttpStatusCode.OK, json);

        var client = CreateClient(handler);

        // Act
        var result = await client.GetWorkflowAsync(workflow.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(workflow.Id, result.Id);
        Assert.Equal(workflow.Name, result.Name);
        Assert.Equal(workflow.Description, result.Description);
        Assert.Equal(workflow.IsActive, result.IsActive);
        Assert.Equal(workflow.Nodes.Count, result.Nodes.Count);
        Assert.Equal(workflow.Connections.Count, result.Connections.Count);
    }


    /// <summary>
    /// Tests that GetWorkflowsAsync returns empty list when API returns empty array.
    /// Validates: Requirements 8.1
    /// </summary>
    [Fact]
    public async Task WhenFetchingWorkflowsAndApiReturnsEmptyArrayThenReturnsEmptyList()
    {
        // Arrange
        var emptyResponse = new PagedWorkflowResponse { Items = [], Skip = 0, Take = 0, TotalCount = 0 };
        var json = JsonSerializer.Serialize(emptyResponse, CamelCaseOptions);
        var handler = new MockHttpMessageHandler()
            .SetupGet("/api/workflow", HttpStatusCode.OK, json);

        var client = CreateClient(handler);

        // Act
        var result = await client.GetWorkflowsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    /// <summary>
    /// Tests that workflow nodes are correctly deserialized with all properties.
    /// Validates: Requirements 8.1
    /// </summary>
    [Fact]
    public async Task WhenFetchingWorkflowThenNodesAreDeserializedWithAllProperties()
    {
        // Arrange
        var workflow = TestFixtures.CreateSimpleWorkflow();
        var response = ToWorkflowResponse(workflow);
        var json = JsonSerializer.Serialize(response, CamelCaseOptions);

        var handler = new MockHttpMessageHandler()
            .SetupGet($"/api/workflow/{workflow.Id}", HttpStatusCode.OK, json);

        var client = CreateClient(handler);

        // Act
        var result = await client.GetWorkflowAsync(workflow.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(workflow.Nodes.Count, result.Nodes.Count);

        for (int i = 0; i < workflow.Nodes.Count; i++)
        {
            var originalNode = workflow.Nodes[i];
            var deserializedNode = result.Nodes.FirstOrDefault(n => n.Id == originalNode.Id);
            Assert.NotNull(deserializedNode);
            Assert.Equal(originalNode.Type, deserializedNode.Type);
            Assert.Equal(originalNode.Name, deserializedNode.Name);
            Assert.Equal(originalNode.Position.X, deserializedNode.Position.X);
            Assert.Equal(originalNode.Position.Y, deserializedNode.Position.Y);
        }
    }

    /// <summary>
    /// Tests that workflow connections are correctly deserialized.
    /// Validates: Requirements 8.1
    /// </summary>
    [Fact]
    public async Task WhenFetchingWorkflowThenConnectionsAreDeserializedCorrectly()
    {
        // Arrange
        var workflow = TestFixtures.CreateSimpleWorkflow();
        var response = ToWorkflowResponse(workflow);
        var json = JsonSerializer.Serialize(response, CamelCaseOptions);

        var handler = new MockHttpMessageHandler()
            .SetupGet($"/api/workflow/{workflow.Id}", HttpStatusCode.OK, json);

        var client = CreateClient(handler);

        // Act
        var result = await client.GetWorkflowAsync(workflow.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(workflow.Connections.Count, result.Connections.Count);

        for (int i = 0; i < workflow.Connections.Count; i++)
        {
            var originalConn = workflow.Connections[i];
            var deserializedConn = result.Connections.FirstOrDefault(c =>
                c.SourceNodeId == originalConn.SourceNodeId &&
                c.TargetNodeId == originalConn.TargetNodeId);
            Assert.NotNull(deserializedConn);
            Assert.Equal(originalConn.SourcePort, deserializedConn.SourcePort);
            Assert.Equal(originalConn.TargetPort, deserializedConn.TargetPort);
        }
    }

    #endregion

    #region Workflow Save Tests (Task 13.1)

    /// <summary>
    /// Tests that CreateWorkflowAsync serializes workflow and sends to API.
    /// Validates: Requirements 8.2
    /// </summary>
    [Fact]
    public async Task WhenSavingWorkflowThenSerializesAndSendsToApi()
    {
        // Arrange
        var workflow = TestFixtures.CreateSimpleWorkflow();
        var response = ToWorkflowResponse(workflow);
        var responseJson = JsonSerializer.Serialize(response, CamelCaseOptions);

        var handler = new MockHttpMessageHandler()
            .SetupPost("/api/workflow", HttpStatusCode.Created, responseJson);

        var client = CreateClient(handler);

        // Act
        var result = await client.CreateWorkflowAsync(workflow);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(workflow.Id, result.Id);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
    }

    /// <summary>
    /// Tests that UpdateWorkflowAsync serializes workflow and sends PUT request.
    /// Validates: Requirements 8.2
    /// </summary>
    [Fact]
    public async Task WhenUpdatingWorkflowThenSerializesAndSendsPutRequest()
    {
        // Arrange
        var workflow = TestFixtures.CreateSimpleWorkflow();
        var response = ToWorkflowResponse(workflow);
        var responseJson = JsonSerializer.Serialize(response, CamelCaseOptions);

        var handler = new MockHttpMessageHandler()
            .SetupPut($"/api/workflow/{workflow.Id}", HttpStatusCode.OK, responseJson);

        var client = CreateClient(handler);

        // Act
        var result = await client.UpdateWorkflowAsync(workflow);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(workflow.Id, result.Id);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Put, handler.LastRequest.Method);
    }

    #endregion

    #region Node Definition Deserialization Tests (Task 13.1)

    /// <summary>
    /// Tests that GetNodeDefinitionsAsync deserializes node definitions correctly.
    /// Validates: Requirements 8.4
    /// </summary>
    [Fact]
    public async Task WhenFetchingNodeDefinitionsThenDeserializesResponseIntoNodeDefinitionObjects()
    {
        // Arrange
        var definitions = TestFixtures.CreateCommonNodeDefinitions();
        var json = JsonSerializer.Serialize(definitions, CamelCaseOptions);

        var handler = new MockHttpMessageHandler()
            .SetupGet("/api/nodes", HttpStatusCode.OK, json);

        var client = CreateClient(handler);

        // Act
        var result = await client.GetNodeDefinitionsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(definitions.Count, result.Count);

        for (int i = 0; i < definitions.Count; i++)
        {
            var original = definitions[i];
            var deserialized = result.FirstOrDefault(d => d.Type == original.Type);
            Assert.NotNull(deserialized);
            Assert.Equal(original.Name, deserialized.Name);
            Assert.Equal(original.Description, deserialized.Description);
            Assert.Equal(original.Category, deserialized.Category);
        }
    }

    /// <summary>
    /// Tests that node definition inputs and outputs are correctly deserialized.
    /// Validates: Requirements 8.4
    /// </summary>
    [Fact]
    public async Task WhenFetchingNodeDefinitionsThenPortsAreDeserializedCorrectly()
    {
        // Arrange
        var definitions = TestFixtures.CreateCommonNodeDefinitions();
        var json = JsonSerializer.Serialize(definitions, CamelCaseOptions);

        var handler = new MockHttpMessageHandler()
            .SetupGet("/api/nodes", HttpStatusCode.OK, json);

        var client = CreateClient(handler);

        // Act
        var result = await client.GetNodeDefinitionsAsync();

        // Assert
        Assert.NotNull(result);

        // Check HttpRequest definition has correct ports
        var httpDef = result.FirstOrDefault(d => d.Type == "HttpRequest");
        Assert.NotNull(httpDef);
        Assert.Single(httpDef.Inputs);
        Assert.Single(httpDef.Outputs);
        Assert.Equal("input", httpDef.Inputs[0].Name);
        Assert.Equal("output", httpDef.Outputs[0].Name);

        // Check If definition has multiple outputs
        var ifDef = result.FirstOrDefault(d => d.Type == "If");
        Assert.NotNull(ifDef);
        Assert.Equal(2, ifDef.Outputs.Count);
    }

    /// <summary>
    /// Tests that GetNodeDefinitionsAsync returns empty list when API returns empty array.
    /// Validates: Requirements 8.4
    /// </summary>
    [Fact]
    public async Task WhenFetchingNodeDefinitionsAndApiReturnsEmptyArrayThenReturnsEmptyList()
    {
        // Arrange
        var handler = new MockHttpMessageHandler()
            .SetupGet("/api/nodes", HttpStatusCode.OK, "[]");

        var client = CreateClient(handler);

        // Act
        var result = await client.GetNodeDefinitionsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion


    #region Error Handling Tests (Task 13.1)

    /// <summary>
    /// Tests that API errors are handled appropriately for GET requests.
    /// Validates: Requirements 8.3
    /// </summary>
    [Fact]
    public async Task WhenApiReturnsNotFoundForWorkflowThenThrowsHttpRequestException()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var handler = new MockHttpMessageHandler()
            .SetupError(HttpMethod.Get, $"/api/workflow/{workflowId}", HttpStatusCode.NotFound, "Workflow not found");

        var client = CreateClient(handler);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetWorkflowAsync(workflowId));
    }

    /// <summary>
    /// Tests that API errors are handled appropriately for POST requests.
    /// Validates: Requirements 8.3
    /// </summary>
    [Fact]
    public async Task WhenApiReturnsBadRequestForCreateThenThrowsApiException()
    {
        // Arrange
        var workflow = TestFixtures.CreateSimpleWorkflow();
        var handler = new MockHttpMessageHandler()
            .SetupError(HttpMethod.Post, "/api/workflow", HttpStatusCode.BadRequest, "Invalid workflow");

        var client = CreateClient(handler);

        // Act & Assert
        await Assert.ThrowsAsync<ApiException>(() => client.CreateWorkflowAsync(workflow));
    }

    /// <summary>
    /// Tests that API errors are handled appropriately for PUT requests.
    /// Validates: Requirements 8.3
    /// </summary>
    [Fact]
    public async Task WhenApiReturnsUnauthorizedForUpdateThenThrowsApiException()
    {
        // Arrange
        var workflow = TestFixtures.CreateSimpleWorkflow();
        var handler = new MockHttpMessageHandler()
            .SetupError(HttpMethod.Put, $"/api/workflow/{workflow.Id}", HttpStatusCode.Unauthorized, "Unauthorized");

        var client = CreateClient(handler);

        // Act & Assert
        await Assert.ThrowsAsync<ApiException>(() => client.UpdateWorkflowAsync(workflow));
    }

    /// <summary>
    /// Tests that API errors are handled appropriately for DELETE requests.
    /// Validates: Requirements 8.3
    /// </summary>
    [Fact]
    public async Task WhenApiReturnsForbiddenForDeleteThenThrowsApiException()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var handler = new MockHttpMessageHandler()
            .SetupError(HttpMethod.Delete, $"/api/workflow/{workflowId}", HttpStatusCode.Forbidden, "Forbidden");

        var client = CreateClient(handler);

        // Act & Assert
        await Assert.ThrowsAsync<ApiException>(() => client.DeleteWorkflowAsync(workflowId));
    }

    /// <summary>
    /// Tests that server errors are handled appropriately.
    /// Validates: Requirements 8.3
    /// </summary>
    [Fact]
    public async Task WhenApiReturnsInternalServerErrorThenThrowsHttpRequestException()
    {
        // Arrange
        var emptyResponse = new PagedWorkflowResponse { Items = [], Skip = 0, Take = 0, TotalCount = 0 };
        var handler = new MockHttpMessageHandler()
            .SetupError(HttpMethod.Get, "/api/workflow", HttpStatusCode.InternalServerError, "Internal server error");

        var client = CreateClient(handler);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetWorkflowsAsync());
    }

    /// <summary>
    /// Tests that delete workflow succeeds with OK status.
    /// Validates: Requirements 8.2
    /// </summary>
    [Fact]
    public async Task WhenDeletingWorkflowAndApiReturnsOkThenSucceeds()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var handler = new MockHttpMessageHandler()
            .SetupDelete($"/api/workflow/{workflowId}", HttpStatusCode.OK);

        var client = CreateClient(handler);

        // Act & Assert - should not throw
        var exception = await Record.ExceptionAsync(() => client.DeleteWorkflowAsync(workflowId));
        Assert.Null(exception);
    }

    /// <summary>
    /// Tests that delete workflow succeeds with NoContent status.
    /// Validates: Requirements 8.2
    /// </summary>
    [Fact]
    public async Task WhenDeletingWorkflowAndApiReturnsNoContentThenSucceeds()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var handler = new MockHttpMessageHandler()
            .SetupDelete($"/api/workflow/{workflowId}", HttpStatusCode.NoContent);

        var client = CreateClient(handler);

        // Act & Assert - should not throw
        var exception = await Record.ExceptionAsync(() => client.DeleteWorkflowAsync(workflowId));
        Assert.Null(exception);
    }

    #endregion

    #region Property Tests (Task 13.2)

    /// <summary>
    /// Feature: blazor-integration-tests, Property 16: API Workflow Deserialization
    /// For any valid JSON workflow response from the API, the FlowForgeApiClient SHALL 
    /// deserialize it into a Workflow object with matching properties.
    /// Validates: Requirements 8.1, 8.4
    /// </summary>
    [Fact]
    public void Property16_ApiWorkflowDeserialization()
    {
        DesignerGenerators.WorkflowGen.Sample(workflow =>
        {
            // Arrange - Convert workflow to WorkflowResponse format (simulating API response)
            var response = ToWorkflowResponse(workflow);
            var json = JsonSerializer.Serialize(response, CamelCaseOptions);

            var handler = new MockHttpMessageHandler()
                .SetupGet($"/api/workflow/{workflow.Id}", HttpStatusCode.OK, json);

            var client = CreateClient(handler);

            // Act
            var result = client.GetWorkflowAsync(workflow.Id).GetAwaiter().GetResult();

            // Assert - Deserialization succeeded
            Assert.NotNull(result);

            // Assert - Core properties match
            Assert.Equal(workflow.Id, result.Id);
            Assert.Equal(workflow.Name, result.Name);
            Assert.Equal(workflow.Description, result.Description);
            Assert.Equal(workflow.Version, result.Version);
            Assert.Equal(workflow.IsActive, result.IsActive);

            // Assert - Node count matches
            Assert.Equal(workflow.Nodes.Count, result.Nodes.Count);

            // Assert - Connection count matches
            Assert.Equal(workflow.Connections.Count, result.Connections.Count);

            // Assert - Tags match
            Assert.Equal(workflow.Tags.Count, result.Tags.Count);
            for (int i = 0; i < workflow.Tags.Count; i++)
            {
                Assert.Equal(workflow.Tags[i], result.Tags[i]);
            }

            // Assert - Settings match
            Assert.Equal(workflow.Settings.MaxRetries, result.Settings.MaxRetries);
            Assert.Equal(workflow.Settings.ErrorHandling, result.Settings.ErrorHandling);
            Assert.Equal(workflow.Settings.Timeout, result.Settings.Timeout);

            // Assert - Node properties match
            for (int i = 0; i < workflow.Nodes.Count; i++)
            {
                var originalNode = workflow.Nodes[i];
                var deserializedNode = result.Nodes.FirstOrDefault(n => n.Id == originalNode.Id);
                Assert.NotNull(deserializedNode);
                Assert.Equal(originalNode.Type, deserializedNode.Type);
                Assert.Equal(originalNode.Name, deserializedNode.Name);
                Assert.Equal(originalNode.Position.X, deserializedNode.Position.X);
                Assert.Equal(originalNode.Position.Y, deserializedNode.Position.Y);
                Assert.Equal(originalNode.CredentialId, deserializedNode.CredentialId);
            }

            // Assert - Connection properties match
            for (int i = 0; i < workflow.Connections.Count; i++)
            {
                var originalConn = workflow.Connections[i];
                var deserializedConn = result.Connections.FirstOrDefault(c =>
                    c.SourceNodeId == originalConn.SourceNodeId &&
                    c.SourcePort == originalConn.SourcePort &&
                    c.TargetNodeId == originalConn.TargetNodeId &&
                    c.TargetPort == originalConn.TargetPort);
                Assert.NotNull(deserializedConn);
            }

            // Assert - CreatedBy matches
            Assert.Equal(workflow.CreatedBy, result.CreatedBy);
        }, iter: 100);
    }

    #endregion
}
