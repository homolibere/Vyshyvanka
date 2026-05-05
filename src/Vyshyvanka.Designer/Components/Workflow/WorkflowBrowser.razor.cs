using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Vyshyvanka.Designer.Components;

public partial class WorkflowBrowser
{
    [Inject] private VyshyvankaApiClient ApiClient { get; set; } = null!;

    [Inject] private NavigationManager Navigation { get; set; } = null!;

    private List<WorkflowSummary> _workflows = [];
    private string _searchQuery = "";
    private bool _isLoading;
    private string? _error;
    private bool _wasOpen;
    private Guid? _confirmDeleteId;

    [Parameter] public bool IsOpen { get; set; }

    [Parameter] public Guid? CurrentWorkflowId { get; set; }

    [Parameter] public EventCallback OnClose { get; set; }

    private IEnumerable<WorkflowSummary> FilteredWorkflows =>
        string.IsNullOrWhiteSpace(_searchQuery)
            ? _workflows
            : _workflows.Where(w =>
                w.Name.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                (w.Description?.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ?? false));

    protected override async Task OnParametersSetAsync()
    {
        if (IsOpen && !_wasOpen && !_isLoading)
        {
            await LoadWorkflowsAsync();
        }

        _wasOpen = IsOpen;
    }

    private async Task LoadWorkflowsAsync()
    {
        _isLoading = true;
        _error = null;
        StateHasChanged();

        try
        {
            var workflows = await ApiClient.GetWorkflowsAsync();
            _workflows = workflows
                .Select(w => new WorkflowSummary(w.Id, w.Name, w.Description, w.Version, w.IsActive, w.UpdatedAt))
                .OrderByDescending(w => w.UpdatedAt)
                .ToList();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private void OnSearchKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            _searchQuery = "";
        }
    }

    private async Task SelectWorkflow(Guid workflowId)
    {
        await OnClose.InvokeAsync();
        Navigation.NavigateTo($"/designer/{workflowId}");
    }

    private async Task CreateNewWorkflow()
    {
        await OnClose.InvokeAsync();
        Navigation.NavigateTo("/designer");
    }

    private async Task Close()
    {
        if (!_isLoading)
        {
            await OnClose.InvokeAsync();
        }
    }

    private async Task HandleOverlayClick()
    {
        await Close();
    }

    private void ConfirmDelete(Guid workflowId)
    {
        _confirmDeleteId = workflowId;
    }

    private void CancelDelete()
    {
        _confirmDeleteId = null;
    }

    private async Task DeleteWorkflowAsync(Guid workflowId)
    {
        _confirmDeleteId = null;

        try
        {
            await ApiClient.DeleteWorkflowAsync(workflowId);
            _workflows.RemoveAll(w => w.Id == workflowId);
        }
        catch (Exception ex)
        {
            _error = $"Failed to delete: {ex.Message}";
        }
    }

    private static string FormatDate(DateTime date)
    {
        var diff = DateTime.UtcNow - date;
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return date.ToString("MMM d, yyyy");
    }

    private record WorkflowSummary(
        Guid Id,
        string Name,
        string? Description,
        int Version,
        bool IsActive,
        DateTime UpdatedAt);
}
