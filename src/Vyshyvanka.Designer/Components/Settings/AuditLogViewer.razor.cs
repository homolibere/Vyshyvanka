using Vyshyvanka.Contracts.Auth;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Components;

public partial class AuditLogViewer
{
    [Inject]
    private AuditLogApiClient AuditClient { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private List<AuditLogResponse> _logs = [];
    private int _totalCount;
    private bool _isLoading;
    private int _skip;
    private const int PageSize = 50;

    // Filters
    private string _selectedEventType = "";
    private string _selectedStatus = "";

    protected override async Task OnInitializedAsync()
    {
        await LoadLogsAsync();
    }

    private async Task LoadLogsAsync()
    {
        _isLoading = true;
        try
        {
            var eventType = string.IsNullOrEmpty(_selectedEventType) ? null : _selectedEventType;
            bool? success = string.IsNullOrEmpty(_selectedStatus)
                ? null
                : bool.Parse(_selectedStatus);

            var result = await AuditClient.GetLogsAsync(eventType, success, _skip, PageSize);
            _logs = result.Logs;
            _totalCount = result.TotalCount;
        }
        catch (ApiException ex)
        {
            ToastService.ShowError($"Failed to load audit logs: {ex.Message}");
            _logs = [];
            _totalCount = 0;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task RefreshAsync()
    {
        _skip = 0;
        await LoadLogsAsync();
    }

    private async Task NextPage()
    {
        _skip += PageSize;
        await LoadLogsAsync();
    }

    private async Task PreviousPage()
    {
        _skip = Math.Max(0, _skip - PageSize);
        await LoadLogsAsync();
    }

    private static string GetEventIcon(string eventType) => eventType switch
    {
        "Authentication" => "fa-right-to-bracket",
        "Authorization" => "fa-shield-halved",
        "WorkflowOperation" => "fa-diagram-project",
        "ExecutionOperation" => "fa-play",
        "CredentialOperation" => "fa-lock",
        "ApiKeyOperation" => "fa-key",
        "UserOperation" => "fa-user",
        _ => "fa-circle-info"
    };

    private static string GetEventBadgeClass(string eventType) => eventType switch
    {
        "Authentication" => "log-badge--auth",
        "Authorization" => "log-badge--authz",
        _ => "log-badge--op"
    };

    private static string FormatEventType(string eventType) => eventType switch
    {
        "Authentication" => "Auth",
        "Authorization" => "Access",
        "WorkflowOperation" => "Workflow",
        "ExecutionOperation" => "Execution",
        "CredentialOperation" => "Credential",
        "ApiKeyOperation" => "API Key",
        "UserOperation" => "User",
        _ => eventType
    };

    private static string FormatTimestamp(DateTime timestamp)
    {
        var diff = DateTime.UtcNow - timestamp;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return timestamp.ToString("MMM d, yyyy HH:mm");
    }
}
