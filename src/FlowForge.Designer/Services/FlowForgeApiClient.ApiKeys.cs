using System.Net.Http.Json;
using FlowForge.Designer.Models;

namespace FlowForge.Designer.Services;

/// <summary>
/// API key management methods for the FlowForge API client.
/// </summary>
public partial class FlowForgeApiClient
{
    /// <summary>Lists all API keys for the current user.</summary>
    public async Task<List<ApiKeyModel>> GetApiKeysAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/apikeys", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return [];

        return await response.Content.ReadFromJsonAsync<List<ApiKeyModel>>(JsonOptions, cancellationToken) ?? [];
    }

    /// <summary>Creates a new API key. The plain-text key is only available in the response.</summary>
    public async Task<CreateApiKeyResponseModel?> CreateApiKeyAsync(
        CreateApiKeyModel model, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/apikeys", model, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CreateApiKeyResponseModel>(JsonOptions, cancellationToken);
    }

    /// <summary>Revokes an API key (deactivates without deleting).</summary>
    public async Task RevokeApiKeyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/apikeys/{id}/revoke", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <summary>Permanently deletes an API key.</summary>
    public async Task DeleteApiKeyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/apikeys/{id}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }
}
