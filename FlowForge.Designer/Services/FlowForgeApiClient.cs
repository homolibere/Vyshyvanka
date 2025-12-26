using System.Net.Http.Json;
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
        var response = await _httpClient.GetFromJsonAsync<List<Workflow>>("api/workflows", cancellationToken);
        return response ?? [];
    }

    /// <summary>Gets a workflow by ID.</summary>
    public async Task<Workflow?> GetWorkflowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<Workflow>($"api/workflows/{id}", cancellationToken);
    }

    /// <summary>Creates a new workflow.</summary>
    public async Task<Workflow?> CreateWorkflowAsync(Workflow workflow, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/workflows", workflow, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Workflow>(cancellationToken);
    }

    /// <summary>Updates an existing workflow.</summary>
    public async Task<Workflow?> UpdateWorkflowAsync(Workflow workflow, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/workflows/{workflow.Id}", workflow, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Workflow>(cancellationToken);
    }

    /// <summary>Deletes a workflow.</summary>
    public async Task DeleteWorkflowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/workflows/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }


    /// <summary>Gets all node definitions from the registry.</summary>
    public async Task<List<NodeDefinition>> GetNodeDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<List<NodeDefinition>>("api/nodes", cancellationToken);
        return response ?? [];
    }

    /// <summary>Triggers a workflow execution.</summary>
    public async Task<Execution?> ExecuteWorkflowAsync(Guid workflowId, object? triggerData = null,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            WorkflowId = workflowId,
            InputData = triggerData,
            Mode = "Api"
        };
        var response = await _httpClient.PostAsJsonAsync("api/executions", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Execution>(cancellationToken);
    }

    /// <summary>Gets execution status.</summary>
    public async Task<Execution?> GetExecutionAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<Execution>($"api/executions/{executionId}", cancellationToken);
    }
}
