using System.Net.Http.Json;
using Vyshyvanka.Designer.Models;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// API client for admin user management operations.
/// </summary>
public class UserApiClient(HttpClient httpClient) : ApiClientBase(httpClient)
{
    /// <summary>Lists users with optional search and pagination.</summary>
    public async Task<UserListResponse> GetUsersAsync(
        string? search = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/user?skip={skip}&take={take}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }

        return await Http.GetFromJsonAsync<UserListResponse>(url, JsonOptions, cancellationToken)
               ?? new UserListResponse();
    }

    /// <summary>Gets a user by ID.</summary>
    public async Task<AdminUserModel?> GetUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Http.GetFromJsonAsync<AdminUserModel>($"api/user/{id}", JsonOptions, cancellationToken);
    }

    /// <summary>Creates a new user (BuiltIn provider only).</summary>
    public async Task<AdminUserModel?> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/user", request, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AdminUserModel>(JsonOptions, cancellationToken);
    }

    /// <summary>Updates a user's role.</summary>
    public async Task<AdminUserModel?> UpdateRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var request = new UpdateUserRoleRequest { Role = role };
        var response = await Http.PutAsJsonAsync($"api/user/{userId}/role", request, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AdminUserModel>(JsonOptions, cancellationToken);
    }

    /// <summary>Activates or deactivates a user.</summary>
    public async Task<AdminUserModel?> UpdateStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var request = new UpdateUserStatusRequest { IsActive = isActive };
        var response = await Http.PutAsJsonAsync($"api/user/{userId}/status", request, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AdminUserModel>(JsonOptions, cancellationToken);
    }

    /// <summary>Updates a user's profile (email and display name).</summary>
    public async Task<AdminUserModel?> UpdateProfileAsync(Guid userId, string email, string? displayName, CancellationToken cancellationToken = default)
    {
        var request = new UpdateUserProfileRequest { Email = email, DisplayName = displayName };
        var response = await Http.PutAsJsonAsync($"api/user/{userId}", request, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AdminUserModel>(JsonOptions, cancellationToken);
    }
}
