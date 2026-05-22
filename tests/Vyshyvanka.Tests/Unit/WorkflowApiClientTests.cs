using System.Net;
using System.Text.Json;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Vyshyvanka.Tests.Unit.Helpers;

namespace Vyshyvanka.Tests.Unit;

public class WorkflowApiClientTests
{
    private static WorkflowApiClient CreateClient(MockHttpHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new WorkflowApiClient(httpClient);
    }

    [Fact]
    public async Task WhenGetWorkflowsAsyncThenCallsCorrectEndpoint()
    {
        var json = """{"items":[],"totalCount":0}""";
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        await sut.GetWorkflowsAsync();

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/workflow");
    }

    [Fact]
    public async Task WhenGetWorkflowsAsyncThenReturnsEmptyListOnEmptyResponse()
    {
        var json = """{"items":[],"totalCount":0}""";
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        var result = await sut.GetWorkflowsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenGetWorkflowAsyncThenCallsCorrectEndpoint()
    {
        var id = Guid.NewGuid();
        var json = $$"""
        {
            "id":"{{id}}","name":"Test","version":1,"isActive":true,
            "nodes":[],"connections":[],"tags":[],
            "createdAt":"2024-01-01T00:00:00Z","updatedAt":"2024-01-01T00:00:00Z"
        }
        """;
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        var result = await sut.GetWorkflowAsync(id);

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be($"/api/workflow/{id}");
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test");
    }

    [Fact]
    public async Task WhenCreateWorkflowAsyncThenPostsToCorrectEndpoint()
    {
        var json = """
        {
            "id":"00000000-0000-0000-0000-000000000001","name":"New","version":1,"isActive":true,
            "nodes":[],"connections":[],"tags":[],
            "createdAt":"2024-01-01T00:00:00Z","updatedAt":"2024-01-01T00:00:00Z"
        }
        """;
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        var workflow = new Vyshyvanka.Core.Models.Workflow { Name = "New", IsActive = true };
        var result = await sut.CreateWorkflowAsync(workflow);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/workflow");
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenUpdateWorkflowAsyncThenPutsToCorrectEndpoint()
    {
        var id = Guid.NewGuid();
        var json = $$"""
        {
            "id":"{{id}}","name":"Updated","version":2,"isActive":true,
            "nodes":[],"connections":[],"tags":[],
            "createdAt":"2024-01-01T00:00:00Z","updatedAt":"2024-01-01T00:00:00Z"
        }
        """;
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        var workflow = new Vyshyvanka.Core.Models.Workflow { Id = id, Name = "Updated", Version = 1 };
        await sut.UpdateWorkflowAsync(workflow);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be($"/api/workflow/{id}");
    }

    [Fact]
    public async Task WhenDeleteWorkflowAsyncThenDeletesCorrectEndpoint()
    {
        var id = Guid.NewGuid();
        var handler = new MockHttpHandler(MockHttpHandler.EmptyResponse(HttpStatusCode.OK));
        var sut = CreateClient(handler);

        await sut.DeleteWorkflowAsync(id);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be($"/api/workflow/{id}");
    }

    [Fact]
    public async Task WhenGetNodeDefinitionsAsyncThenReturnsDefinitions()
    {
        var json = """[{"type":"if","name":"If","category":2,"inputs":[],"outputs":[]}]""";
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        var result = await sut.GetNodeDefinitionsAsync();

        result.Should().HaveCount(1);
        result[0].Type.Should().Be("if");
    }

    [Fact]
    public async Task WhenExecuteWorkflowAsyncThenPostsToExecutionEndpoint()
    {
        var json = """{"id":"00000000-0000-0000-0000-000000000001","status":1,"nodeExecutions":[]}""";
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        var result = await sut.ExecuteWorkflowAsync(Guid.NewGuid());

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/execution");
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenGetExecutionAsyncThenGetsCorrectEndpoint()
    {
        var id = Guid.NewGuid();
        var json = $$"""{"id":"{{id}}","status":2,"nodeExecutions":[]}""";
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        var result = await sut.GetExecutionAsync(id);

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be($"/api/execution/{id}");
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenApiReturnsErrorThenThrowsApiException()
    {
        var handler = new MockHttpHandler(MockHttpHandler.ErrorResponse("VALIDATION_FAILED", "Name is required"));
        var sut = CreateClient(handler);

        var workflow = new Vyshyvanka.Core.Models.Workflow { Name = "" };

        var act = () => sut.CreateWorkflowAsync(workflow);

        await act.Should().ThrowAsync<ApiException>();
    }

    [Fact]
    public async Task WhenExecuteSingleNodeAsyncThenPostsToNodeEndpoint()
    {
        var json = """{"nodeId":"n1","status":2,"startedAt":"2024-01-01T00:00:00Z"}""";
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        var inputData = JsonDocument.Parse("{}").RootElement;
        var result = await sut.ExecuteSingleNodeAsync(Guid.NewGuid(), "n1", inputData);

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/execution/node");
        result.Should().NotBeNull();
    }
}
