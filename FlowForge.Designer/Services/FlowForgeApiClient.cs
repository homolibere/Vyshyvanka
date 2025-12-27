using System.Net.Http.Json;
using System.Text.Json;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using FlowForge.Designer.Models;

namespace FlowForge.Designer.Services;

/// <summary>
/// HTTP client for communicating with the FlowForge API.
/// </summary>
public partial class FlowForgeApiClient
{
    private readonly HttpClient _httpClient;

    public FlowForgeApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>Gets all workflows.</summary>
    public async Task<List<Workflow>> GetWorkflowsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<PagedWorkflowResponse>("api/workflow", cancellationToken);
        return response?.Items.Select(MapToWorkflow).ToList() ?? [];
    }

    /// <summary>Gets a workflow by ID.</summary>
    public async Task<Workflow?> GetWorkflowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<WorkflowResponse>($"api/workflow/{id}", cancellationToken);
        return response is null ? null : MapToWorkflow(response);
    }

    /// <summary>Creates a new workflow.</summary>
    public async Task<Workflow?> CreateWorkflowAsync(Workflow workflow, CancellationToken cancellationToken = default)
    {
        var request = MapToCreateRequest(workflow);
        var response = await _httpClient.PostAsJsonAsync("api/workflow", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<WorkflowResponse>(cancellationToken);
        return result is null ? null : MapToWorkflow(result);
    }

    /// <summary>Updates an existing workflow.</summary>
    public async Task<Workflow?> UpdateWorkflowAsync(Workflow workflow, CancellationToken cancellationToken = default)
    {
        var request = MapToUpdateRequest(workflow);
        var response = await _httpClient.PutAsJsonAsync($"api/workflow/{workflow.Id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<WorkflowResponse>(cancellationToken);
        return result is null ? null : MapToWorkflow(result);
    }

    /// <summary>Deletes a workflow.</summary>
    public async Task DeleteWorkflowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/workflow/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
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
        Configuration = node.Configuration,
        Position = new PositionDto(node.Position.X, node.Position.Y),
        CredentialId = node.CredentialId
    };

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
            ErrorHandling = settings.ErrorHandling
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
        Settings = response.Settings is not null ? new WorkflowSettings
        {
            Timeout = response.Settings.TimeoutSeconds.HasValue
                ? TimeSpan.FromSeconds(response.Settings.TimeoutSeconds.Value)
                : null,
            MaxRetries = response.Settings.MaxRetries,
            ErrorHandling = response.Settings.ErrorHandling
        } : new WorkflowSettings(),
        Tags = response.Tags,
        CreatedAt = response.CreatedAt,
        UpdatedAt = response.UpdatedAt,
        CreatedBy = response.CreatedBy
    };


    /// <summary>Gets all node definitions from the registry.</summary>
    public async Task<List<NodeDefinition>> GetNodeDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<List<NodeDefinition>>("api/nodes", cancellationToken);
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
        var response = await _httpClient.PostAsJsonAsync("api/execution", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExecutionResponse>(cancellationToken);
    }

    /// <summary>Gets execution status.</summary>
    public async Task<ExecutionResponse?> GetExecutionAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<ExecutionResponse>($"api/execution/{executionId}", cancellationToken);
    }
}
