using System.Net.Http.Json;
using Vyshyvanka.Designer.Models;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// API client for folder CRUD operations.
/// </summary>
public class FolderApiClient(HttpClient httpClient) : ApiClientBase(httpClient)
{
    /// <summary>Gets all folders for the current user.</summary>
    public async Task<List<FolderResponse>> GetFoldersAsync(CancellationToken cancellationToken = default)
    {
        return await Http.GetFromJsonAsync<List<FolderResponse>>("api/folder", JsonOptions, cancellationToken) ?? [];
    }

    /// <summary>Gets a folder by ID.</summary>
    public async Task<FolderResponse?> GetFolderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Http.GetFromJsonAsync<FolderResponse>($"api/folder/{id}", JsonOptions, cancellationToken);
    }

    /// <summary>Creates a new folder.</summary>
    public async Task<FolderResponse?> CreateFolderAsync(CreateFolderRequest request, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/folder", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<FolderResponse>(JsonOptions, cancellationToken);
    }

    /// <summary>Updates a folder.</summary>
    public async Task<FolderResponse?> UpdateFolderAsync(Guid id, UpdateFolderRequest request, CancellationToken cancellationToken = default)
    {
        var response = await Http.PutAsJsonAsync($"api/folder/{id}", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<FolderResponse>(JsonOptions, cancellationToken);
    }

    /// <summary>Deletes a folder. Workflows in the folder are moved to root.</summary>
    public async Task DeleteFolderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.DeleteAsync($"api/folder/{id}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }
}
