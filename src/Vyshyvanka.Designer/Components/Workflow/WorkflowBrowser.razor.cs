using System.Text.Json;
using System.Text.Json.Serialization;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Vyshyvanka.Designer.Components;

public partial class WorkflowBrowser
{
    private static readonly JsonSerializerOptions ExportOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Inject] private WorkflowApiClient ApiClient { get; set; } = null!;

    [Inject] private NavigationManager Navigation { get; set; } = null!;

    [Inject] private IJSRuntime JS { get; set; } = null!;

    [Inject] private ToastService Toast { get; set; } = null!;

    private List<WorkflowSummary> _workflows = [];
    private string _searchQuery = "";
    private bool _isLoading;
    private string? _error;
    private bool _wasOpen;
    private Guid? _confirmDeleteId;
    private Workflow? _importConfirmWorkflow;

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

    private async Task ExportWorkflow(Guid workflowId, string workflowName)
    {
        try
        {
            var workflow = await ApiClient.GetWorkflowAsync(workflowId);
            if (workflow is null)
            {
                Toast.ShowError("Workflow not found", "Export Failed");
                return;
            }

            var json = JsonSerializer.Serialize(workflow, ExportOptions);
            var safeName = string.Join("_", workflowName.Split(Path.GetInvalidFileNameChars()));
            var filename = $"{safeName}.json";

            await JS.InvokeVoidAsync("downloadFile", filename, json, "application/json");
            Toast.ShowSuccess("Workflow exported", "Export");
        }
        catch (Exception ex)
        {
            Toast.ShowError($"Export failed: {ex.Message}", "Export Failed");
        }
    }

    private async Task ImportWorkflow()
    {
        try
        {
            var json = await JS.InvokeAsync<string?>("triggerFileUpload", ".json");
            if (string.IsNullOrWhiteSpace(json))
                return;

            var workflow = WorkflowStore.DeserializeFromJson(json);
            if (workflow is null)
            {
                Toast.ShowError("Invalid workflow JSON file", "Import Failed");
                return;
            }

            // Check if a workflow with this ID already exists
            var existing = _workflows.FirstOrDefault(w => w.Id == workflow.Id);
            if (existing is not null)
            {
                // Show overwrite confirmation
                _importConfirmWorkflow = workflow;
                StateHasChanged();
                return;
            }

            await CreateImportedWorkflowAsync(workflow);
        }
        catch (JsonException)
        {
            Toast.ShowError("File is not valid JSON", "Import Failed");
        }
        catch (Exception ex)
        {
            Toast.ShowError($"Import failed: {ex.Message}", "Import Failed");
        }
    }

    private async Task ConfirmImportOverwrite()
    {
        if (_importConfirmWorkflow is null)
            return;

        var workflow = _importConfirmWorkflow;
        _importConfirmWorkflow = null;

        try
        {
            await ApiClient.UpdateWorkflowAsync(workflow);
            Toast.ShowSuccess($"Workflow \"{workflow.Name}\" overwritten", "Import");
            await LoadWorkflowsAsync();
        }
        catch (Exception ex)
        {
            Toast.ShowError($"Import failed: {ex.Message}", "Import Failed");
        }
    }

    private void CancelImport()
    {
        _importConfirmWorkflow = null;
    }

    private async Task ImportAsCopy()
    {
        if (_importConfirmWorkflow is null)
            return;

        var workflow = _importConfirmWorkflow with
        {
            Id = Guid.NewGuid(),
            Name = $"{_importConfirmWorkflow.Name} (imported)",
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _importConfirmWorkflow = null;

        await CreateImportedWorkflowAsync(workflow);
    }

    private async Task CreateImportedWorkflowAsync(Workflow workflow)
    {
        try
        {
            await ApiClient.CreateWorkflowAsync(workflow);
            Toast.ShowSuccess($"Workflow \"{workflow.Name}\" imported", "Import");
            await LoadWorkflowsAsync();
        }
        catch (Exception ex)
        {
            Toast.ShowError($"Import failed: {ex.Message}", "Import Failed");
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
