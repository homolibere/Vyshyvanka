using System.Text.Json;
using System.Text.Json.Serialization;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Designer.Models;
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
    [Inject] private FolderApiClient FolderClient { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private ToastService Toast { get; set; } = null!;

    private List<WorkflowSummary> _workflows = [];
    private List<WorkflowSummary> _sharedWorkflows = [];
    private List<FolderResponse> _folders = [];
    private string _searchQuery = "";
    private bool _isLoading;
    private string? _error;
    private bool _wasOpen;
    private Guid? _confirmDeleteId;
    private Workflow? _importConfirmWorkflow;

    // Folder state
    private BrowserSection _activeSection = BrowserSection.All;
    private Guid? _activeFolderId;
    private Guid? _folderMenuId;
    private bool _showFolderInput;
    private string _folderNameInput = "";
    private FolderResponse? _renamingFolder;
    private bool _showDeleteFolderConfirm;
    private Guid? _deletingFolderId;

    // Share dialog state
    private Guid? _shareDialogWorkflowId;
    private string _shareDialogWorkflowName = "";

    // Move-to-folder state
    private Guid? _actionMenuWorkflowId;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public Guid? CurrentWorkflowId { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private IEnumerable<WorkflowSummary> DisplayedWorkflows
    {
        get
        {
            var source = _activeSection == BrowserSection.Shared ? _sharedWorkflows : GetFilteredByFolder();

            if (string.IsNullOrWhiteSpace(_searchQuery))
                return source;

            return source.Where(w =>
                w.Name.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                (w.Description?.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ?? false));
        }
    }

    private IEnumerable<WorkflowSummary> GetFilteredByFolder()
    {
        if (_activeSection == BrowserSection.Folder && _activeFolderId is not null)
            return _workflows.Where(w => w.FolderId == _activeFolderId);

        return _workflows; // All
    }

    protected override async Task OnParametersSetAsync()
    {
        if (IsOpen && !_wasOpen && !_isLoading)
        {
            // Reset transient UI state when reopening
            _actionMenuWorkflowId = null;
            _confirmDeleteId = null;
            _folderMenuId = null;
            _showFolderInput = false;
            _showDeleteFolderConfirm = false;
            _shareDialogWorkflowId = null;

            await LoadAllAsync();
        }

        _wasOpen = IsOpen;
    }

    private async Task LoadAllAsync()
    {
        _isLoading = true;
        _error = null;
        StateHasChanged();

        try
        {
            var workflowsTask = ApiClient.GetWorkflowsAsync();
            var foldersTask = FolderClient.GetFoldersAsync();

            await Task.WhenAll(workflowsTask, foldersTask);

            var workflows = await workflowsTask;
            _workflows = workflows
                .Select(w => new WorkflowSummary(w.Id, w.Name, w.Description, w.Version, w.IsActive, w.UpdatedAt, w.FolderId))
                .OrderByDescending(w => w.UpdatedAt)
                .ToList();

            _folders = await foldersTask;

            // TODO: load shared workflows when API supports "shared with me" listing
            _sharedWorkflows = [];
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

    private async Task LoadWorkflowsAsync()
    {
        await LoadAllAsync();
    }

    // --- Section navigation ---

    private void SelectSection(BrowserSection section)
    {
        _activeSection = section;
        _activeFolderId = null;
        _folderMenuId = null;
    }

    private void SelectFolder(Guid folderId)
    {
        _activeSection = BrowserSection.Folder;
        _activeFolderId = folderId;
        _folderMenuId = null;
    }

    // --- Folder CRUD ---

    private void ToggleFolderMenu(Guid folderId)
    {
        _folderMenuId = _folderMenuId == folderId ? null : folderId;
    }

    private void StartCreateFolder()
    {
        _renamingFolder = null;
        _folderNameInput = "";
        _showFolderInput = true;
        _folderMenuId = null;
    }

    private void StartRenameFolder(FolderResponse folder)
    {
        _renamingFolder = folder;
        _folderNameInput = folder.Name;
        _showFolderInput = true;
        _folderMenuId = null;
    }

    private void CancelFolderInput()
    {
        _showFolderInput = false;
        _renamingFolder = null;
        _folderNameInput = "";
    }

    private async Task OnFolderInputKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            await SaveFolderAsync();
        else if (e.Key == "Escape")
            CancelFolderInput();
    }

    private async Task SaveFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(_folderNameInput))
            return;

        try
        {
            if (_renamingFolder is not null)
            {
                await FolderClient.UpdateFolderAsync(_renamingFolder.Id, new UpdateFolderRequest
                {
                    Name = _folderNameInput.Trim(),
                    Color = _renamingFolder.Color
                });
                Toast.ShowSuccess($"Folder renamed to \"{_folderNameInput.Trim()}\"", "Folder");
            }
            else
            {
                await FolderClient.CreateFolderAsync(new CreateFolderRequest
                {
                    Name = _folderNameInput.Trim()
                });
                Toast.ShowSuccess($"Folder \"{_folderNameInput.Trim()}\" created", "Folder");
            }

            _showFolderInput = false;
            _renamingFolder = null;
            _folderNameInput = "";
            _folders = await FolderClient.GetFoldersAsync();
        }
        catch (Exception ex)
        {
            Toast.ShowError(ex.Message, "Folder Error");
        }
    }

    private void ConfirmDeleteFolder(Guid folderId)
    {
        _deletingFolderId = folderId;
        _showDeleteFolderConfirm = true;
        _folderMenuId = null;
    }

    private void CancelDeleteFolder()
    {
        _showDeleteFolderConfirm = false;
        _deletingFolderId = null;
    }

    private async Task DeleteFolderAsync()
    {
        if (_deletingFolderId is null) return;

        try
        {
            await FolderClient.DeleteFolderAsync(_deletingFolderId.Value);
            Toast.ShowSuccess("Folder deleted", "Folder");

            if (_activeFolderId == _deletingFolderId)
                SelectSection(BrowserSection.All);

            _showDeleteFolderConfirm = false;
            _deletingFolderId = null;
            await LoadAllAsync();
        }
        catch (Exception ex)
        {
            Toast.ShowError(ex.Message, "Folder Error");
        }
    }

    // --- Share dialog ---

    private void OpenShareDialog(Guid workflowId, string workflowName)
    {
        _actionMenuWorkflowId = null;
        _shareDialogWorkflowId = workflowId;
        _shareDialogWorkflowName = workflowName;
    }

    private void CloseShareDialog()
    {
        _shareDialogWorkflowId = null;
        _shareDialogWorkflowName = "";
    }

    // --- Move to folder ---

    private void ToggleActionMenu(Guid workflowId)
    {
        _actionMenuWorkflowId = _actionMenuWorkflowId == workflowId ? null : workflowId;
    }

    private async Task MoveWorkflowAsync(Guid workflowId, Guid? folderId)
    {
        _actionMenuWorkflowId = null;

        try
        {
            await ApiClient.MoveToFolderAsync(workflowId, folderId);

            // Update local state
            var idx = _workflows.FindIndex(w => w.Id == workflowId);
            if (idx >= 0)
            {
                var w = _workflows[idx];
                _workflows[idx] = w with { FolderId = folderId };
            }

            // Refresh folder counts
            _folders = await FolderClient.GetFoldersAsync();

            var folderName = folderId is null ? "root" : _folders.FirstOrDefault(f => f.Id == folderId)?.Name ?? "folder";
            Toast.ShowSuccess($"Moved to {folderName}", "Workflow");
        }
        catch (Exception ex)
        {
            Toast.ShowError(ex.Message, "Move Failed");
        }
    }

    // --- Workflow operations (existing) ---

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
        _actionMenuWorkflowId = null;
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
        _actionMenuWorkflowId = null;

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

            var existing = _workflows.FirstOrDefault(w => w.Id == workflow.Id);
            if (existing is not null)
            {
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
            await LoadAllAsync();
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
            await LoadAllAsync();
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
        DateTime UpdatedAt,
        Guid? FolderId);

    private enum BrowserSection
    {
        All,
        Folder,
        Shared
    }
}
