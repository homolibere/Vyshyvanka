using System.Net.Http.Json;
using FlowForge.Designer.Models;

namespace FlowForge.Designer.Services;

/// <summary>
/// Credential management methods for the FlowForge API client.
/// </summary>
public partial class FlowForgeApiClient
{
    /// <summary>Lists all credentials for the current user.</summary>
    public async Task<List<CredentialModel>> GetCredentialsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/credentials", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return [];

        return await response.Content.ReadFromJsonAsync<List<CredentialModel>>(JsonOptions, cancellationToken) ?? [];
    }

    /// <summary>Gets a credential by ID.</summary>
    public async Task<CredentialModel?> GetCredentialAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/credentials/{id}", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<CredentialModel>(JsonOptions, cancellationToken);
    }

    /// <summary>Creates a new credential.</summary>
    public async Task<CredentialModel?> CreateCredentialAsync(
        CreateCredentialModel model, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/credentials", model, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CredentialModel>(JsonOptions, cancellationToken);
    }

    /// <summary>Updates an existing credential.</summary>
    public async Task<CredentialModel?> UpdateCredentialAsync(
        Guid id, UpdateCredentialModel model, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/credentials/{id}", model, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CredentialModel>(JsonOptions, cancellationToken);
    }

    /// <summary>Deletes a credential.</summary>
    public async Task DeleteCredentialAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/credentials/{id}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }
}
