using Vyshyvanka.Core.Enums;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Components;

public partial class ExecutionHistoryPanel : IDisposable
{
    [Inject] private WorkflowApiClient ApiClient { get; set; } = null!;
    [Inject] private WorkflowStore Store { get; set; } = null!;
    [Inject] private ExecutionStateService ExecutionState { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private AuthStateService AuthState { get; set; } = null!;

    /// <summary>Raised when the user selects an execution to inspect.</summary>
    [Parameter] public EventCallback<ExecutionResponse> OnExecutionSelected { get; set; }

    /// <summary>Raised when the user deselects the current execution.</summary>
    [Parameter] public EventCallback OnExecutionCleared { get; set; }

    /// <summary>When true, loads all executions regardless of current workflow. Use on Home page.</summary>
    [Parameter] public bool ShowAll { get; set; }

    private List<ExecutionSummaryResponse> _executions = [];
    private int _totalCount;
    private bool _isLoading;
    private string? _loadError;
    private Guid? _selectedExecutionId;
    private const int PageSize = 50;

    protected override async Task OnInitializedAsync()
    {
        ExecutionState.OnExecutionChanged += HandleExecutionChanged;
        await LoadHistoryAsync();
    }

    /// <summary>Reloads the execution history from the API.</summary>
    public async Task RefreshAsync()
    {
        _executions.Clear();
        await LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        var workflowId = Store.Workflow.Id;

        _isLoading = true;
        _loadError = null;
        StateHasChanged();

        try
        {
            var result = (ShowAll || workflowId == Guid.Empty)
                ? await ApiClient.GetExecutionHistoryAsync(skip: 0, take: PageSize)
                : await ApiClient.GetExecutionHistoryAsync(workflowId, skip: 0, take: PageSize);
            _executions = result.Items.ToList();
            _totalCount = result.TotalCount;
        }
        catch (Exception ex)
        {
            _loadError = $"{ex.GetType().Name}: {ex.Message}";
            System.Console.WriteLine($"[ExecutionHistoryPanel] LoadHistoryAsync failed: {_loadError}");
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadMoreAsync()
    {
        var workflowId = Store.Workflow.Id;

        _isLoading = true;
        StateHasChanged();

        try
        {
            var result = (ShowAll || workflowId == Guid.Empty)
                ? await ApiClient.GetExecutionHistoryAsync(skip: _executions.Count, take: PageSize)
                : await ApiClient.GetExecutionHistoryAsync(workflowId, skip: _executions.Count, take: PageSize);
            _executions.AddRange(result.Items);
            _totalCount = result.TotalCount;
        }
        catch
        {
            // Silently handle
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task SelectExecution(Guid executionId)
    {
        // If no handler bound (standalone mode on Home page), navigate to Designer
        if (!OnExecutionSelected.HasDelegate)
        {
            var execution = _executions.FirstOrDefault(e => e.Id == executionId);
            if (execution is not null)
            {
                Navigation.NavigateTo($"/designer/{execution.WorkflowId}?execution={executionId}");
            }
            return;
        }

        if (_selectedExecutionId == executionId)
        {
            // Deselect
            _selectedExecutionId = null;
            ExecutionState.ClearExecutionState();
            await OnExecutionCleared.InvokeAsync();
            return;
        }

        _selectedExecutionId = executionId;
        StateHasChanged();

        try
        {
            var fullExecution = await ApiClient.GetExecutionAsync(executionId);
            if (fullExecution is not null)
            {
                ExecutionState.SetCurrentExecution(fullExecution);
                await OnExecutionSelected.InvokeAsync(fullExecution);
            }
        }
        catch
        {
            _selectedExecutionId = null;
        }
    }

    private void HandleExecutionChanged(ExecutionResponse? execution)
    {
        if (execution is null)
        {
            _selectedExecutionId = null;
        }
        else if (execution.Status is ExecutionStatus.Completed or ExecutionStatus.Failed or ExecutionStatus.Cancelled)
        {
            // A live execution just finished — refresh the list to include it
            _ = InvokeAsync(async () =>
            {
                await RefreshAsync();
                StateHasChanged();
            });
        }

        InvokeAsync(StateHasChanged);
    }

    private bool IsSelected(Guid executionId) => _selectedExecutionId == executionId;

    private static string GetStatusClass(ExecutionStatus status) => status switch
    {
        ExecutionStatus.Completed => "status-completed",
        ExecutionStatus.Failed => "status-failed",
        ExecutionStatus.Running => "status-running",
        ExecutionStatus.Pending => "status-pending",
        ExecutionStatus.Cancelled => "status-cancelled",
        _ => ""
    };

    private static string GetStatusIcon(ExecutionStatus status) => status switch
    {
        ExecutionStatus.Completed => "✓",
        ExecutionStatus.Failed => "✗",
        ExecutionStatus.Running => "●",
        ExecutionStatus.Pending => "○",
        ExecutionStatus.Cancelled => "⊘",
        _ => "?"
    };

    private static string GetModeIcon(ExecutionMode mode) => mode switch
    {
        ExecutionMode.Trigger => "🔗",
        ExecutionMode.Scheduled => "⏰",
        ExecutionMode.Manual => "👤",
        ExecutionMode.Api => "⚡",
        _ => ""
    };

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMilliseconds < 1000)
            return $"{duration.TotalMilliseconds:0}ms";
        if (duration.TotalSeconds < 60)
            return $"{duration.TotalSeconds:0.#}s";
        if (duration.TotalMinutes < 60)
            return $"{duration.TotalMinutes:0.#}m";
        return $"{duration.TotalHours:0.#}h";
    }

    private static string FormatRelativeTime(DateTime utcTime)
    {
        var elapsed = DateTime.UtcNow - utcTime;

        if (elapsed.TotalSeconds < 60) return "just now";
        if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
        if (elapsed.TotalDays < 7) return $"{(int)elapsed.TotalDays}d ago";

        return utcTime.ToString("MMM dd, HH:mm");
    }

    private static string TruncateError(string error)
    {
        const int maxLength = 60;
        return error.Length <= maxLength ? error : string.Concat(error.AsSpan(0, maxLength), "...");
    }

    public void Dispose()
    {
        ExecutionState.OnExecutionChanged -= HandleExecutionChanged;
    }
}
