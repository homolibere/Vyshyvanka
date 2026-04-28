using System.Text.Json;
using Vyshyvanka.Api.Models;
using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Tests.Integration.Fixtures;

/// <summary>
/// Factory for creating test data objects for integration tests.
/// </summary>
public static class TestDataFactory
{
    /// <summary>
    /// Creates a valid workflow request with a manual trigger node.
    /// </summary>
    /// <param name="name">Workflow name (defaults to "Test Workflow")</param>
    /// <param name="isActive">Whether the workflow is active (defaults to true)</param>
    /// <returns>A valid CreateWorkflowRequest with a trigger node</returns>
    public static CreateWorkflowRequest CreateValidWorkflow(
        string name = "Test Workflow",
        bool isActive = true)
    {
        return new CreateWorkflowRequest
        {
            Name = name,
            Description = $"Test workflow: {name}",
            IsActive = isActive,
            Nodes =
            [
                new WorkflowNodeDto
                {
                    Id = "trigger-1",
                    Type = "manual-trigger",
                    Name = "Manual Trigger",
                    Position = new PositionDto(100, 100),
                    Configuration = JsonSerializer.SerializeToElement(new { })
                }
            ],
            Connections = [],
            Settings = new WorkflowSettingsDto
            {
                TimeoutSeconds = 300,
                MaxRetries = 3,
                ErrorHandling = ErrorHandlingMode.StopOnFirstError
            },
            Tags = ["test"]
        };
    }

    /// <summary>
    /// Creates a valid workflow request with a webhook trigger node.
    /// </summary>
    public static CreateWorkflowRequest CreateWebhookWorkflow(
        string name = "Webhook Workflow",
        bool isActive = true)
    {
        return new CreateWorkflowRequest
        {
            Name = name,
            Description = $"Webhook workflow: {name}",
            IsActive = isActive,
            Nodes =
            [
                new WorkflowNodeDto
                {
                    Id = "trigger-1",
                    Type = "webhook-trigger",
                    Name = "Webhook Trigger",
                    Position = new PositionDto(100, 100),
                    Configuration = JsonSerializer.SerializeToElement(new { })
                }
            ],
            Connections = [],
            Settings = new WorkflowSettingsDto
            {
                TimeoutSeconds = 300,
                MaxRetries = 3,
                ErrorHandling = ErrorHandlingMode.StopOnFirstError
            },
            Tags = ["test", "webhook"]
        };
    }

    /// <summary>
    /// Creates a valid workflow request with a manual trigger and an HTTP request action node.
    /// </summary>
    public static CreateWorkflowRequest CreateValidWorkflowWithAction(
        string name = "Test Workflow with Action",
        bool isActive = true)
    {
        return new CreateWorkflowRequest
        {
            Name = name,
            Description = $"Test workflow with action: {name}",
            IsActive = isActive,
            Nodes =
            [
                new WorkflowNodeDto
                {
                    Id = "trigger-1",
                    Type = "manual-trigger",
                    Name = "Manual Trigger",
                    Position = new PositionDto(100, 100),
                    Configuration = JsonSerializer.SerializeToElement(new { })
                },
                new WorkflowNodeDto
                {
                    Id = "action-1",
                    Type = "http-request",
                    Name = "HTTP Request",
                    Position = new PositionDto(300, 100),
                    Configuration = JsonSerializer.SerializeToElement(new
                    {
                        url = "https://api.example.com/test",
                        method = "GET"
                    })
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
            ],
            Settings = new WorkflowSettingsDto
            {
                TimeoutSeconds = 300,
                MaxRetries = 3,
                ErrorHandling = ErrorHandlingMode.StopOnFirstError
            },
            Tags = ["test", "http"]
        };
    }

    /// <summary>
    /// Creates an invalid workflow request without a trigger node.
    /// This should fail validation as every workflow must have exactly one trigger.
    /// </summary>
    /// <returns>An invalid CreateWorkflowRequest without a trigger node</returns>
    public static CreateWorkflowRequest CreateInvalidWorkflow()
    {
        return new CreateWorkflowRequest
        {
            Name = "Invalid Workflow",
            Description = "This workflow has no trigger node",
            IsActive = true,
            Nodes =
            [
                new WorkflowNodeDto
                {
                    Id = "action-1",
                    Type = "http-request",
                    Name = "HTTP Request",
                    Position = new PositionDto(100, 100),
                    Configuration = JsonSerializer.SerializeToElement(new
                    {
                        url = "https://api.example.com/test",
                        method = "GET"
                    })
                }
            ],
            Connections = [],
            Settings = new WorkflowSettingsDto
            {
                TimeoutSeconds = 300,
                MaxRetries = 3,
                ErrorHandling = ErrorHandlingMode.StopOnFirstError
            },
            Tags = ["test", "invalid"]
        };
    }

    /// <summary>
    /// Creates an execution request for the specified workflow.
    /// </summary>
    /// <param name="workflowId">The ID of the workflow to execute</param>
    /// <param name="inputData">Optional input data for the execution</param>
    /// <returns>A TriggerExecutionRequest for the workflow</returns>
    public static TriggerExecutionRequest CreateExecutionRequest(
        Guid workflowId,
        object? inputData = null)
    {
        return new TriggerExecutionRequest
        {
            WorkflowId = workflowId,
            InputData = inputData is not null
                ? JsonSerializer.SerializeToElement(inputData)
                : null,
            Mode = ExecutionMode.Api
        };
    }

    /// <summary>
    /// Creates an update workflow request from an existing workflow response.
    /// </summary>
    /// <param name="existing">The existing workflow response</param>
    /// <param name="newName">Optional new name for the workflow</param>
    /// <returns>An UpdateWorkflowRequest based on the existing workflow</returns>
    public static UpdateWorkflowRequest CreateUpdateRequest(
        WorkflowResponse existing,
        string? newName = null)
    {
        return new UpdateWorkflowRequest
        {
            Name = newName ?? existing.Name,
            Description = existing.Description,
            IsActive = existing.IsActive,
            Nodes = existing.Nodes,
            Connections = existing.Connections,
            Settings = existing.Settings,
            Tags = existing.Tags,
            Version = existing.Version
        };
    }
}
