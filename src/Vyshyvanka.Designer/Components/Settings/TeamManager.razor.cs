using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Components;

public partial class TeamManager
{
    [Inject]
    private TeamApiClient TeamClient { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private List<TeamResponse> _teams = [];
    private bool _isLoading;

    // Create form
    private bool _showCreateForm;
    private string _formName = "";
    private string _formDescription = "";
    private bool _isSaving;
    private string? _formError;

    // Expand/collapse
    private Guid? _expandedTeamId;

    // Edit modal
    private TeamResponse? _editTeam;
    private string _editName = "";
    private string _editDescription = "";
    private string? _editError;

    // Delete confirmation
    private Guid? _confirmDeleteId;

    protected override async Task OnInitializedAsync()
    {
        await LoadTeamsAsync();
    }

    private async Task LoadTeamsAsync()
    {
        _isLoading = true;
        try
        {
            _teams = await TeamClient.GetTeamsAsync();
        }
        catch
        {
            _teams = [];
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ToggleExpand(Guid teamId)
    {
        _expandedTeamId = _expandedTeamId == teamId ? null : teamId;
    }

    // Create
    private void ShowCreateForm()
    {
        _showCreateForm = true;
        _formName = "";
        _formDescription = "";
        _formError = null;
    }

    private void CloseForm()
    {
        _showCreateForm = false;
        _formError = null;
    }

    private async Task HandleCreate()
    {
        if (string.IsNullOrWhiteSpace(_formName))
        {
            _formError = "Team name is required.";
            return;
        }

        _isSaving = true;
        _formError = null;

        try
        {
            var request = new CreateTeamRequest
            {
                Name = _formName.Trim(),
                Description = string.IsNullOrWhiteSpace(_formDescription) ? null : _formDescription.Trim()
            };

            await TeamClient.CreateTeamAsync(request);
            ToastService.ShowSuccess($"Team '{request.Name}' created");
            CloseForm();
            await LoadTeamsAsync();
        }
        catch (ApiException ex)
        {
            _formError = ex.Message;
        }
        catch (Exception ex)
        {
            _formError = $"Failed to create team: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    // Edit
    private void StartEdit(TeamResponse team)
    {
        _editTeam = team;
        _editName = team.Name;
        _editDescription = team.Description ?? "";
        _editError = null;
    }

    private void CloseEdit()
    {
        _editTeam = null;
        _editError = null;
    }

    private async Task HandleUpdate()
    {
        if (_editTeam is null) return;

        if (string.IsNullOrWhiteSpace(_editName))
        {
            _editError = "Team name is required.";
            return;
        }

        _isSaving = true;
        _editError = null;

        try
        {
            var request = new UpdateTeamRequest
            {
                Name = _editName.Trim(),
                Description = string.IsNullOrWhiteSpace(_editDescription) ? null : _editDescription.Trim()
            };

            await TeamClient.UpdateTeamAsync(_editTeam.Id, request);
            ToastService.ShowSuccess($"Team '{request.Name}' updated");
            CloseEdit();
            await LoadTeamsAsync();
        }
        catch (ApiException ex)
        {
            _editError = ex.Message;
        }
        catch (Exception ex)
        {
            _editError = $"Failed to update team: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    // Delete
    private void RequestDelete(Guid teamId)
    {
        _confirmDeleteId = teamId;
    }

    private void CancelAction()
    {
        _confirmDeleteId = null;
    }

    private async Task ConfirmDelete()
    {
        if (_confirmDeleteId is null) return;

        var id = _confirmDeleteId.Value;
        var name = _teams.FirstOrDefault(t => t.Id == id)?.Name ?? "team";
        _confirmDeleteId = null;

        try
        {
            await TeamClient.DeleteTeamAsync(id);
            ToastService.ShowSuccess($"Team '{name}' deleted");
            await LoadTeamsAsync();
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Failed to delete: {ex.Message}");
        }
    }

    // Remove member
    private async Task RemoveMember(Guid teamId, Guid userId)
    {
        try
        {
            await TeamClient.RemoveMemberAsync(teamId, userId);
            ToastService.ShowSuccess("Member removed");
            await LoadTeamsAsync();
        }
        catch (ApiException ex)
        {
            ToastService.ShowError($"Failed to remove member: {ex.Message}");
        }
    }
}
