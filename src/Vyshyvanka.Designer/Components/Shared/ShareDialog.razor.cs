using Vyshyvanka.Core.Enums;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Components;

public partial class ShareDialog
{
    [Inject] private SharingApiClient SharingClient { get; set; } = null!;
    [Inject] private TeamApiClient TeamClient { get; set; } = null!;
    [Inject] private ToastService Toast { get; set; } = null!;

    [Parameter, EditorRequired] public Guid WorkflowId { get; set; }
    [Parameter] public string WorkflowName { get; set; } = "";
    [Parameter] public EventCallback OnClose { get; set; }

    private List<WorkflowPermissionResponse> _permissions = [];
    private List<TeamResponse> _teams = [];
    private string _targetType = "User";
    private string _targetInput = "";
    private string _selectedTeamId = "";
    private string _permissionLevel = "View";
    private string _credentialPolicy = "UseOwnerCredentials";
    private string? _errorMessage;

    private bool CanShare => _targetType == "User"
        ? !string.IsNullOrWhiteSpace(_targetInput)
        : !string.IsNullOrWhiteSpace(_selectedTeamId);

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var permissionsTask = SharingClient.GetPermissionsAsync(WorkflowId);
            var teamsTask = TeamClient.GetTeamsAsync();
            await Task.WhenAll(permissionsTask, teamsTask);

            _permissions = await permissionsTask;
            _teams = await teamsTask;
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
    }

    private async Task ShareAsync()
    {
        _errorMessage = null;

        try
        {
            Guid targetId;
            PermissionTargetType targetType;

            if (_targetType == "User")
            {
                if (!Guid.TryParse(_targetInput.Trim(), out targetId))
                {
                    _errorMessage = "Please enter a valid user ID";
                    return;
                }
                targetType = PermissionTargetType.User;
            }
            else
            {
                if (!Guid.TryParse(_selectedTeamId, out targetId))
                {
                    _errorMessage = "Please select a team";
                    return;
                }
                targetType = PermissionTargetType.Team;
            }

            var level = Enum.Parse<WorkflowPermissionLevel>(_permissionLevel);
            var policy = Enum.Parse<CredentialSharingPolicy>(_credentialPolicy);

            var request = new ShareWorkflowRequest
            {
                TargetType = targetType,
                TargetId = targetId,
                PermissionLevel = level,
                CredentialPolicy = policy
            };

            await SharingClient.ShareAsync(WorkflowId, request);
            Toast.ShowSuccess("Workflow shared successfully", "Sharing");

            _targetInput = "";
            _selectedTeamId = "";
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
    }

    private async Task RevokeAsync(Guid permissionId)
    {
        try
        {
            await SharingClient.RevokeAsync(WorkflowId, permissionId);
            Toast.ShowSuccess("Permission revoked", "Sharing");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
    }

    private async Task Close()
    {
        await OnClose.InvokeAsync();
    }

    private async Task HandleOverlayClick()
    {
        await Close();
    }

    private void SetCredentialPolicyOwner() => _credentialPolicy = "UseOwnerCredentials";
    private void SetCredentialPolicyOwn() => _credentialPolicy = "RequireOwnCredentials";
}
