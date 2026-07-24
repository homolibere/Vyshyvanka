using System.Net.Http.Json;
using Vyshyvanka.Contracts.Sharing;
using Vyshyvanka.Designer.Models;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// API client for workflow sharing operations.
/// </summary>
public class SharingApiClient(HttpClient httpClient) : ApiClientBase(httpClient)
{
    /// <summary>Gets all permissions for a workflow.</summary>
    public async Task<List<WorkflowPermissionResponse>> GetPermissionsAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        return await Http.GetFromJsonAsync<List<WorkflowPermissionResponse>>(
            $"api/workflow/{workflowId}/sharing", JsonOptions, cancellationToken) ?? [];
    }

    /// <summary>Shares a workflow with a user or team.</summary>
    public async Task<WorkflowPermissionResponse?> ShareAsync(Guid workflowId, ShareWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync($"api/workflow/{workflowId}/sharing", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<WorkflowPermissionResponse>(JsonOptions, cancellationToken);
    }

    /// <summary>Revokes a permission grant.</summary>
    public async Task RevokeAsync(Guid workflowId, Guid permissionId, CancellationToken cancellationToken = default)
    {
        var response = await Http.DeleteAsync($"api/workflow/{workflowId}/sharing/{permissionId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }
}
