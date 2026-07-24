using System.Net.Http.Json;
using Vyshyvanka.Contracts.Auth;
using Vyshyvanka.Designer.Models;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// API client for API key management operations.
/// </summary>
public class ApiKeyApiClient(HttpClient httpClient) : ApiClientBase(httpClient)
{
    /// <summary>Lists all API keys for the current user.</summary>
    public async Task<List<ApiKeyResponse>> GetApiKeysAsync(CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync("api/apikeys", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return [];

        return await response.Content.ReadFromJsonAsync<List<ApiKeyResponse>>(JsonOptions, cancellationToken) ?? [];
    }

    /// <summary>Creates a new API key. The plain-text key is only available in the response.</summary>
    public async Task<CreateApiKeyResponse?> CreateApiKeyAsync(
        CreateApiKeyRequest model, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/apikeys", model, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>(JsonOptions, cancellationToken);
    }

    /// <summary>Revokes an API key (deactivates without deleting).</summary>
    public async Task RevokeApiKeyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsync($"api/apikeys/{id}/revoke", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <summary>Permanently deletes an API key.</summary>
    public async Task DeleteApiKeyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.DeleteAsync($"api/apikeys/{id}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }
}
