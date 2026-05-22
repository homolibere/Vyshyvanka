using System.Net.Http.Json;
using Vyshyvanka.Designer.Models;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// API client for credential CRUD operations.
/// </summary>
public class CredentialApiClient(HttpClient httpClient) : ApiClientBase(httpClient)
{
    /// <summary>Lists all credentials for the current user.</summary>
    public async Task<List<CredentialModel>> GetCredentialsAsync(CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync("api/credentials", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return [];

        return await response.Content.ReadFromJsonAsync<List<CredentialModel>>(JsonOptions, cancellationToken) ?? [];
    }

    /// <summary>Gets a credential by ID.</summary>
    public async Task<CredentialModel?> GetCredentialAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/credentials/{id}", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<CredentialModel>(JsonOptions, cancellationToken);
    }

    /// <summary>Creates a new credential.</summary>
    public async Task<CredentialModel?> CreateCredentialAsync(
        CreateCredentialModel model, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/credentials", model, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CredentialModel>(JsonOptions, cancellationToken);
    }

    /// <summary>Updates an existing credential.</summary>
    public async Task<CredentialModel?> UpdateCredentialAsync(
        Guid id, UpdateCredentialModel model, CancellationToken cancellationToken = default)
    {
        var response = await Http.PutAsJsonAsync($"api/credentials/{id}", model, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CredentialModel>(JsonOptions, cancellationToken);
    }

    /// <summary>Deletes a credential.</summary>
    public async Task DeleteCredentialAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.DeleteAsync($"api/credentials/{id}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }
}
