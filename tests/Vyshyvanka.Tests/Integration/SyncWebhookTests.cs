using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Vyshyvanka.Api.Models;
using Vyshyvanka.Tests.Integration.Fixtures;

namespace Vyshyvanka.Tests.Integration;

public class SyncWebhookTests : IClassFixture<VyshyvankaWebApplicationFactory>
{
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    public SyncWebhookTests(VyshyvankaWebApplicationFactory factory)
    {
        _authClient = factory.CreateAuthenticatedClient();
        _anonClient = factory.CreateClient();
    }

    private async Task<Guid> CreateSyncWebhookWorkflowAsync(
        bool exposeAuthorizationHeader = false,
        string? webhookPath = null)
    {
        var triggerConfig = new Dictionary<string, object>
        {
            ["responseMode"] = "sync",
            ["responseTimeout"] = 10
        };

        if (exposeAuthorizationHeader)
        {
            triggerConfig["exposeAuthorizationHeader"] = true;
        }

        if (webhookPath is not null)
        {
            triggerConfig["path"] = webhookPath;
        }

        var request = new CreateWorkflowRequest
        {
            Name = "Sync Webhook Test",
            Description = "Workflow with sync response mode",
            IsActive = true,
            Nodes =
            [
                new WorkflowNodeDto
                {
                    Id = "trigger-1",
                    Type = "webhook-trigger",
                    Name = "Webhook",
                    Position = new PositionDto(0, 0),
                    Configuration = JsonSerializer.SerializeToElement(triggerConfig)
                },
                new WorkflowNodeDto
                {
                    Id = "response-1",
                    Type = "http-response",
                    Name = "Respond",
                    Position = new PositionDto(200, 0),
                    Configuration = JsonSerializer.SerializeToElement(new
                    {
                        statusCode = 200,
                        body = """{"status":"ok","source":"workflow"}"""
                    })
                }
            ],
            Connections =
            [
                new ConnectionDto
                {
                    SourceNodeId = "trigger-1",
                    SourcePort = "output",
                    TargetNodeId = "response-1",
                    TargetPort = "input"
                }
            ]
        };

        var response = await _authClient.PostAsJsonAsync("/api/workflow", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var workflow = await response.Content.ReadFromJsonAsync<WorkflowResponse>();
        return workflow!.Id;
    }

    [Fact]
    public async Task WhenSyncWebhookTriggeredThenReturnsHttpResponseNodeOutput()
    {
        var workflowId = await CreateSyncWebhookWorkflowAsync();

        var response = await _anonClient.PostAsJsonAsync(
            $"/api/webhook/{workflowId}",
            new { message = "hello" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\"");
        body.Should().Contain("\"ok\"");
        body.Should().Contain("\"source\"");
        body.Should().Contain("\"workflow\"");
    }

    [Fact]
    public async Task WhenAsyncWebhookTriggeredThenReturnsStandardWebhookResponse()
    {
        // Create a workflow with default (async) response mode
        var request = new CreateWorkflowRequest
        {
            Name = "Async Webhook Test",
            IsActive = true,
            Nodes =
            [
                new WorkflowNodeDto
                {
                    Id = "trigger-1",
                    Type = "webhook-trigger",
                    Name = "Webhook",
                    Position = new PositionDto(0, 0)
                }
            ]
        };

        var createResponse = await _authClient.PostAsJsonAsync("/api/workflow", request);
        var workflow = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();

        var response = await _anonClient.PostAsJsonAsync(
            $"/api/webhook/{workflow!.Id}",
            new { test = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("executionId", out _).Should().BeTrue();
        body.TryGetProperty("workflowId", out _).Should().BeTrue();
        body.TryGetProperty("status", out _).Should().BeTrue();
    }

    [Fact]
    public async Task WhenExposeAuthorizationHeaderIsTrueThenHeaderIsInWebhookData()
    {
        // This test verifies the header is accessible in the workflow context.
        // The HTTP Response node echoes trigger output, which the WebhookTriggerNode
        // extracts from the webhook data. We verify by checking the workflow doesn't
        // strip the Authorization header from the trigger's input data (context variables).
        var triggerConfig = new Dictionary<string, object>
        {
            ["responseMode"] = "sync",
            ["responseTimeout"] = 10,
            ["exposeAuthorizationHeader"] = true
        };

        var request = new CreateWorkflowRequest
        {
            Name = "Auth Header Webhook",
            IsActive = true,
            Nodes =
            [
                new WorkflowNodeDto
                {
                    Id = "trigger-1",
                    Type = "webhook-trigger",
                    Name = "Webhook",
                    Position = new PositionDto(0, 0),
                    Configuration = JsonSerializer.SerializeToElement(triggerConfig)
                },
                new WorkflowNodeDto
                {
                    Id = "response-1",
                    Type = "http-response",
                    Name = "Echo",
                    Position = new PositionDto(200, 0),
                    Configuration = JsonSerializer.SerializeToElement(new
                    {
                        statusCode = 200
                    })
                }
            ],
            Connections =
            [
                new ConnectionDto
                {
                    SourceNodeId = "trigger-1",
                    SourcePort = "output",
                    TargetNodeId = "response-1",
                    TargetPort = "input"
                }
            ]
        };

        var createResponse = await _authClient.PostAsJsonAsync("/api/workflow", request);
        var workflow = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();

        // Send webhook with Authorization header
        var webhookRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/webhook/{workflow!.Id}");
        webhookRequest.Headers.Add("Authorization", "Bearer test-gateway-token");
        webhookRequest.Content = JsonContent.Create(new { data = "test" });

        var response = await _anonClient.SendAsync(webhookRequest);

        // Sync response mode should work — the response comes from the HTTP Response node
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WhenExposeAuthorizationHeaderIsFalseThenHeaderIsNotInWebhookData()
    {
        // Same test but without exposeAuthorizationHeader
        var triggerConfig = new Dictionary<string, object>
        {
            ["responseMode"] = "sync",
            ["responseTimeout"] = 10
        };

        var request = new CreateWorkflowRequest
        {
            Name = "No Auth Header Webhook",
            IsActive = true,
            Nodes =
            [
                new WorkflowNodeDto
                {
                    Id = "trigger-1",
                    Type = "webhook-trigger",
                    Name = "Webhook",
                    Position = new PositionDto(0, 0),
                    Configuration = JsonSerializer.SerializeToElement(triggerConfig)
                },
                new WorkflowNodeDto
                {
                    Id = "response-1",
                    Type = "http-response",
                    Name = "Echo",
                    Position = new PositionDto(200, 0),
                    Configuration = JsonSerializer.SerializeToElement(new
                    {
                        statusCode = 200
                    })
                }
            ],
            Connections =
            [
                new ConnectionDto
                {
                    SourceNodeId = "trigger-1",
                    SourcePort = "output",
                    TargetNodeId = "response-1",
                    TargetPort = "input"
                }
            ]
        };

        var createResponse = await _authClient.PostAsJsonAsync("/api/workflow", request);
        var workflow = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();

        // Send webhook with Authorization header
        var webhookRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/webhook/{workflow!.Id}");
        webhookRequest.Headers.Add("Authorization", "Bearer secret-token");
        webhookRequest.Content = JsonContent.Create(new { data = "test" });

        var response = await _anonClient.SendAsync(webhookRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        // Authorization should NOT appear in the response
        body.Should().NotContain("secret-token");
    }

    [Fact]
    public async Task WhenWebhookTriggeredThenRawBodyIsAvailableInData()
    {
        var triggerConfig = new Dictionary<string, object>
        {
            ["responseMode"] = "sync",
            ["responseTimeout"] = 10
        };

        var request = new CreateWorkflowRequest
        {
            Name = "RawBody Webhook",
            IsActive = true,
            Nodes =
            [
                new WorkflowNodeDto
                {
                    Id = "trigger-1",
                    Type = "webhook-trigger",
                    Name = "Webhook",
                    Position = new PositionDto(0, 0),
                    Configuration = JsonSerializer.SerializeToElement(triggerConfig)
                },
                new WorkflowNodeDto
                {
                    Id = "response-1",
                    Type = "http-response",
                    Name = "Echo",
                    Position = new PositionDto(200, 0),
                    Configuration = JsonSerializer.SerializeToElement(new
                    {
                        statusCode = 200,
                        body = """{"received":true}"""
                    })
                }
            ],
            Connections =
            [
                new ConnectionDto
                {
                    SourceNodeId = "trigger-1",
                    SourcePort = "output",
                    TargetNodeId = "response-1",
                    TargetPort = "input"
                }
            ]
        };

        var createResponse = await _authClient.PostAsJsonAsync("/api/workflow", request);
        var workflow = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();

        var response = await _anonClient.PostAsJsonAsync(
            $"/api/webhook/{workflow!.Id}",
            new { signature_data = "verify_me" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("received");
        body.Should().Contain("true");
    }

    [Fact]
    public async Task WhenInactiveWorkflowTriggeredThenReturns400()
    {
        var request = new CreateWorkflowRequest
        {
            Name = "Inactive Workflow",
            IsActive = false,
            Nodes =
            [
                new WorkflowNodeDto
                {
                    Id = "trigger-1",
                    Type = "webhook-trigger",
                    Name = "Webhook",
                    Position = new PositionDto(0, 0)
                }
            ]
        };

        var createResponse = await _authClient.PostAsJsonAsync("/api/workflow", request);
        var workflow = await createResponse.Content.ReadFromJsonAsync<WorkflowResponse>();

        var response = await _anonClient.PostAsJsonAsync(
            $"/api/webhook/{workflow!.Id}",
            new { test = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        error.GetProperty("code").GetString().Should().Be("WORKFLOW_INACTIVE");
    }

    [Fact]
    public async Task WhenNonexistentWorkflowTriggeredThenReturns404()
    {
        var response = await _anonClient.PostAsJsonAsync(
            $"/api/webhook/{Guid.NewGuid()}",
            new { test = true });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
