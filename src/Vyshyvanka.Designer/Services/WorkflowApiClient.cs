using System.Net.Http.Json;
using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Designer.Models;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// API client for workflow CRUD, execution, and node definitions.
/// </summary>
public class WorkflowApiClient(HttpClient httpClient) : ApiClientBase(httpClient)
{
    /// <summary>Gets all workflows.</summary>
    public async Task<List<Workflow>> GetWorkflowsAsync(CancellationToken cancellationToken = default)
    {
        var response = await Http.GetFromJsonAsync<PagedWorkflowResponse>("api/workflow", cancellationToken);
        return response?.Items.Select(MapToWorkflow).ToList() ?? [];
    }

    /// <summary>Gets a workflow by ID.</summary>
    public async Task<Workflow?> GetWorkflowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.GetFromJsonAsync<WorkflowResponse>($"api/workflow/{id}", cancellationToken);
        return response is null ? null : MapToWorkflow(response);
    }

    /// <summary>Creates a new workflow.</summary>
    public async Task<Workflow?> CreateWorkflowAsync(Workflow workflow, CancellationToken cancellationToken = default)
    {
        var request = MapToCreateRequest(workflow);
        var response = await Http.PostAsJsonAsync("api/workflow", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<WorkflowResponse>(cancellationToken);
        return result is null ? null : MapToWorkflow(result);
    }

    /// <summary>Updates an existing workflow.</summary>
    public async Task<Workflow?> UpdateWorkflowAsync(Workflow workflow, CancellationToken cancellationToken = default)
    {
        var request = MapToUpdateRequest(workflow);
        var response = await Http.PutAsJsonAsync($"api/workflow/{workflow.Id}", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<WorkflowResponse>(cancellationToken);
        return result is null ? null : MapToWorkflow(result);
    }

    /// <summary>Deletes a workflow.</summary>
    public async Task DeleteWorkflowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.DeleteAsync($"api/workflow/{id}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <summary>Gets all node definitions from the registry.</summary>
    public async Task<List<NodeDefinition>> GetNodeDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        var response =
            await Http.GetFromJsonAsync<List<NodeDefinition>>("api/nodes", JsonOptions, cancellationToken);
        return response ?? [];
    }

    /// <summary>Triggers a workflow execution.</summary>
    public async Task<ExecutionResponse?> ExecuteWorkflowAsync(Guid workflowId, JsonElement? triggerData = null,
        CancellationToken cancellationToken = default)
    {
        var request = new TriggerExecutionRequest
        {
            WorkflowId = workflowId,
            InputData = triggerData,
            Mode = ExecutionMode.Api
        };
        var response = await Http.PostAsJsonAsync("api/execution", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ExecutionResponse>(cancellationToken);
    }

    /// <summary>Executes a workflow from the trigger up to and including the specified node.</summary>
    public async Task<ExecutionResponse?> ExecuteUpToNodeAsync(Guid workflowId, string targetNodeId,
        JsonElement? triggerData = null, bool includeTargetNode = true, CancellationToken cancellationToken = default)
    {
        var request = new TriggerExecutionRequest
        {
            WorkflowId = workflowId,
            InputData = triggerData,
            Mode = ExecutionMode.Api,
            TargetNodeId = targetNodeId,
            IncludeTargetNode = includeTargetNode
        };
        var response = await Http.PostAsJsonAsync("api/execution", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ExecutionResponse>(cancellationToken);
    }

    /// <summary>Executes a single node with provided input data (no full workflow run).</summary>
    public async Task<NodeExecutionResponse?> ExecuteSingleNodeAsync(Guid workflowId, string nodeId,
        JsonElement inputData, CancellationToken cancellationToken = default)
    {
        var request = new ExecuteNodeRequest
        {
            WorkflowId = workflowId,
            NodeId = nodeId,
            InputData = inputData
        };
        var response = await Http.PostAsJsonAsync("api/execution/node", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<NodeExecutionResponse>(cancellationToken);
    }

    /// <summary>Gets execution status.</summary>
    public async Task<ExecutionResponse?> GetExecutionAsync(Guid executionId,
        CancellationToken cancellationToken = default)
    {
        return await Http.GetFromJsonAsync<ExecutionResponse>($"api/execution/{executionId}", cancellationToken);
    }

    private static CreateWorkflowRequest MapToCreateRequest(Workflow workflow) => new()
    {
        Name = workflow.Name,
        Description = workflow.Description,
        IsActive = workflow.IsActive,
        Nodes = workflow.Nodes.Select(MapToNodeDto).ToList(),
        Connections = workflow.Connections.Select(MapToConnectionDto).ToList(),
        Settings = MapToSettingsDto(workflow.Settings),
        Tags = workflow.Tags
    };

    private static UpdateWorkflowRequest MapToUpdateRequest(Workflow workflow) => new()
    {
        Name = workflow.Name,
        Description = workflow.Description,
        IsActive = workflow.IsActive,
        Nodes = workflow.Nodes.Select(MapToNodeDto).ToList(),
        Connections = workflow.Connections.Select(MapToConnectionDto).ToList(),
        Settings = MapToSettingsDto(workflow.Settings),
        Tags = workflow.Tags,
        Version = workflow.Version
    };

    private static WorkflowNodeDto MapToNodeDto(WorkflowNode node) => new()
    {
        Id = node.Id,
        Type = node.Type,
        Name = node.Name,
        Configuration = IsValidJsonElement(node.Configuration) ? node.Configuration : null,
        Position = new PositionDto(node.Position.X, node.Position.Y),
        CredentialId = node.CredentialId
    };

    private static bool IsValidJsonElement(JsonElement element)
    {
        return element.ValueKind != JsonValueKind.Undefined;
    }

    private static ConnectionDto MapToConnectionDto(Connection conn) => new()
    {
        SourceNodeId = conn.SourceNodeId,
        SourcePort = conn.SourcePort,
        TargetNodeId = conn.TargetNodeId,
        TargetPort = conn.TargetPort
    };

    private static WorkflowSettingsDto? MapToSettingsDto(WorkflowSettings? settings)
    {
        if (settings is null) return null;
        return new WorkflowSettingsDto
        {
            TimeoutSeconds = settings.Timeout.HasValue ? (int)settings.Timeout.Value.TotalSeconds : null,
            MaxRetries = settings.MaxRetries,
            ErrorHandling = settings.ErrorHandling,
            MaxDegreeOfParallelism = settings.MaxDegreeOfParallelism
        };
    }

    private static Workflow MapToWorkflow(WorkflowResponse response) => new()
    {
        Id = response.Id,
        Name = response.Name,
        Description = response.Description,
        Version = response.Version,
        IsActive = response.IsActive,
        Nodes = response.Nodes.Select(n => new WorkflowNode
        {
            Id = n.Id,
            Type = n.Type,
            Name = n.Name,
            Configuration = n.Configuration ?? default,
            Position = new Position(n.Position.X, n.Position.Y),
            CredentialId = n.CredentialId
        }).ToList(),
        Connections = response.Connections.Select(c => new Connection
        {
            SourceNodeId = c.SourceNodeId,
            SourcePort = c.SourcePort,
            TargetNodeId = c.TargetNodeId,
            TargetPort = c.TargetPort
        }).ToList(),
        Settings = response.Settings is not null
            ? new WorkflowSettings
            {
                Timeout = response.Settings.TimeoutSeconds.HasValue
                    ? TimeSpan.FromSeconds(response.Settings.TimeoutSeconds.Value)
                    : null,
                MaxRetries = response.Settings.MaxRetries,
                ErrorHandling = response.Settings.ErrorHandling
            }
            : new WorkflowSettings(),
        Tags = response.Tags,
        FolderId = response.FolderId,
        CreatedAt = response.CreatedAt,
        UpdatedAt = response.UpdatedAt,
        CreatedBy = response.CreatedBy
    };
}
