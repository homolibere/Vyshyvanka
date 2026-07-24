using System.Net.Http.Json;
using Vyshyvanka.Contracts.Credentials;
using Vyshyvanka.Designer.Models;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// API client for credential CRUD operations.
/// </summary>
public class CredentialApiClient(HttpClient httpClient) : ApiClientBase(httpClient)
{
    /// <summary>Lists all credentials for the current user.</summary>
    public async Task<List<CredentialResponse>> GetCredentialsAsync(CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync("api/credentials", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return [];

        return await response.Content.ReadFromJsonAsync<List<CredentialResponse>>(JsonOptions, cancellationToken) ?? [];
    }

    /// <summary>Gets a credential by ID.</summary>
    public async Task<CredentialResponse?> GetCredentialAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/credentials/{id}", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<CredentialResponse>(JsonOptions, cancellationToken);
    }

    /// <summary>Creates a new credential.</summary>
    public async Task<CredentialResponse?> CreateCredentialAsync(
        CreateCredentialRequest model, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/credentials", model, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CredentialResponse>(JsonOptions, cancellationToken);
    }

    /// <summary>Updates an existing credential.</summary>
    public async Task<CredentialResponse?> UpdateCredentialAsync(
        Guid id, UpdateCredentialRequest model, CancellationToken cancellationToken = default)
    {
        var response = await Http.PutAsJsonAsync($"api/credentials/{id}", model, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CredentialResponse>(JsonOptions, cancellationToken);
    }

    /// <summary>Deletes a credential.</summary>
    public async Task DeleteCredentialAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.DeleteAsync($"api/credentials/{id}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }
}
