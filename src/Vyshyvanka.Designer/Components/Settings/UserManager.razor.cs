using System.Net.Http.Json;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Vyshyvanka.Designer.Components;

public partial class UserManager
{
    [Inject]
    private UserApiClient UserClient { get; set; } = null!;

    [Inject]
    private TeamApiClient TeamClient { get; set; } = null!;

    [Inject]
    private HttpClient Http { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private List<AdminUserModel> _users = [];
    private int _totalCount;
    private bool _isLoading;
    private string _searchTerm = "";
    private int _skip;
    private const int _pageSize = 50;

    // Create form state
    private bool _showCreateForm;
    private string _formEmail = "";
    private string _formDisplayName = "";
    private string _formPassword = "";
    private string _formRole = "Editor";
    private bool _isSaving;
    private string? _formError;

    // Auth config
    private bool _canCreateUsers;

    // Team assignment
    private AdminUserModel? _teamAssignUser;
    private List<TeamResponse> _teams = [];
    private string? _teamError;

    protected override async Task OnInitializedAsync()
    {
        await LoadAuthConfigAsync();
        await LoadUsersAsync();
    }

    private async Task LoadAuthConfigAsync()
    {
        try
        {
            var config = await Http.GetFromJsonAsync<AuthConfigModel>("api/auth/config");
            _canCreateUsers = string.Equals(config?.Provider, "BuiltIn", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            _canCreateUsers = false;
        }
    }

    private async Task LoadUsersAsync()
    {
        _isLoading = true;
        try
        {
            var result = await UserClient.GetUsersAsync(_searchTerm, _skip, _pageSize);
            _users = result.Users;
            _totalCount = result.TotalCount;
        }
        catch (ApiException ex)
        {
            ToastService.ShowError($"Failed to load users: {ex.Message}");
            _users = [];
            _totalCount = 0;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SearchAsync()
    {
        _skip = 0;
        await LoadUsersAsync();
    }

    private async Task OnSearchKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await SearchAsync();
        }
    }

    private async Task NextPage()
    {
        _skip += _pageSize;
        await LoadUsersAsync();
    }

    private async Task PreviousPage()
    {
        _skip = Math.Max(0, _skip - _pageSize);
        await LoadUsersAsync();
    }

    // Create form
    private void ShowCreateForm()
    {
        _showCreateForm = true;
        _formEmail = "";
        _formDisplayName = "";
        _formPassword = "";
        _formRole = "Editor";
        _formError = null;
    }

    private void CloseForm()
    {
        _showCreateForm = false;
        _formError = null;
    }

    private async Task HandleCreate()
    {
        if (string.IsNullOrWhiteSpace(_formEmail))
        {
            _formError = "Email is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_formPassword) || _formPassword.Length < 8)
        {
            _formError = "Password must be at least 8 characters.";
            return;
        }

        _isSaving = true;
        _formError = null;

        try
        {
            var request = new CreateUserRequest
            {
                Email = _formEmail.Trim(),
                Password = _formPassword,
                DisplayName = string.IsNullOrWhiteSpace(_formDisplayName) ? null : _formDisplayName.Trim(),
                Role = _formRole
            };

            await UserClient.CreateUserAsync(request);
            ToastService.ShowSuccess($"User '{request.Email}' created");
            CloseForm();
            await LoadUsersAsync();
        }
        catch (ApiException ex)
        {
            _formError = ex.Message;
        }
        catch (Exception ex)
        {
            _formError = $"Failed to create user: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    // Role change
    private async Task HandleRoleChange(Guid userId, ChangeEventArgs e)
    {
        var newRole = e.Value?.ToString();
        if (string.IsNullOrEmpty(newRole)) return;

        try
        {
            await UserClient.UpdateRoleAsync(userId, newRole);
            ToastService.ShowSuccess($"Role updated to {newRole}");
            await LoadUsersAsync();
        }
        catch (ApiException ex)
        {
            ToastService.ShowError($"Failed to update role: {ex.Message}");
        }
    }

    // Status toggle
    private async Task ToggleStatus(Guid userId, bool activate)
    {
        try
        {
            await UserClient.UpdateStatusAsync(userId, activate);
            ToastService.ShowSuccess(activate ? "User activated" : "User deactivated");
            await LoadUsersAsync();
        }
        catch (ApiException ex)
        {
            ToastService.ShowError($"Failed to update status: {ex.Message}");
        }
    }

    // Team assignment
    private async Task ShowTeamAssign(AdminUserModel user)
    {
        _teamAssignUser = user;
        _teamError = null;

        try
        {
            _teams = await TeamClient.GetTeamsAsync();
        }
        catch
        {
            _teams = [];
            _teamError = "Failed to load teams.";
        }

        StateHasChanged();
    }

    private void CloseTeamAssign()
    {
        _teamAssignUser = null;
        _teamError = null;
    }

    private async Task AddToTeam(Guid teamId, Guid userId)
    {
        _teamError = null;
        try
        {
            await TeamClient.AddMemberAsync(teamId, new AddTeamMemberRequest { UserId = userId });
            ToastService.ShowSuccess("User added to team");
            // Refresh teams to update membership display
            _teams = await TeamClient.GetTeamsAsync();
        }
        catch (ApiException ex)
        {
            _teamError = ex.Message;
        }
    }

    private async Task RemoveFromTeam(Guid teamId, Guid userId)
    {
        _teamError = null;
        try
        {
            await TeamClient.RemoveMemberAsync(teamId, userId);
            ToastService.ShowSuccess("User removed from team");
            _teams = await TeamClient.GetTeamsAsync();
        }
        catch (ApiException ex)
        {
            _teamError = ex.Message;
        }
    }
}
