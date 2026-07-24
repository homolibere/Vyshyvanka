using System.Net.Http.Json;
using Vyshyvanka.Contracts.Teams;
using Vyshyvanka.Designer.Models;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// API client for team management operations.
/// </summary>
public class TeamApiClient(HttpClient httpClient) : ApiClientBase(httpClient)
{
    /// <summary>Gets all teams the current user belongs to.</summary>
    public async Task<List<TeamResponse>> GetTeamsAsync(CancellationToken cancellationToken = default)
    {
        return await Http.GetFromJsonAsync<List<TeamResponse>>("api/team", JsonOptions, cancellationToken) ?? [];
    }

    /// <summary>Gets a team by ID.</summary>
    public async Task<TeamResponse?> GetTeamAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Http.GetFromJsonAsync<TeamResponse>($"api/team/{id}", JsonOptions, cancellationToken);
    }

    /// <summary>Creates a new team.</summary>
    public async Task<TeamResponse?> CreateTeamAsync(CreateTeamRequest request, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/team", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TeamResponse>(JsonOptions, cancellationToken);
    }

    /// <summary>Updates a team.</summary>
    public async Task<TeamResponse?> UpdateTeamAsync(Guid id, UpdateTeamRequest request, CancellationToken cancellationToken = default)
    {
        var response = await Http.PutAsJsonAsync($"api/team/{id}", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TeamResponse>(JsonOptions, cancellationToken);
    }

    /// <summary>Deletes a team.</summary>
    public async Task DeleteTeamAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.DeleteAsync($"api/team/{id}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <summary>Adds a member to a team.</summary>
    public async Task AddMemberAsync(Guid teamId, AddTeamMemberRequest request, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync($"api/team/{teamId}/members", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <summary>Removes a member from a team.</summary>
    public async Task RemoveMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default)
    {
        var response = await Http.DeleteAsync($"api/team/{teamId}/members/{userId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }
}
