using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Designer.Components;
using Vyshyvanka.Designer.Layout;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Pages;

public partial class Designer : IAsyncDisposable
{
    [Inject] private WorkflowStore Store { get; set; } = null!;

    [Inject] private WorkflowEditService EditService { get; set; } = null!;

    [Inject] private WorkflowValidationService ValidationService { get; set; } = null!;

    [Inject] private ExecutionStateService ExecutionState { get; set; } = null!;

    [Inject] private WorkflowApiClient ApiClient { get; set; } = null!;

    [Inject] private NavigationManager Navigation { get; set; } = null!;

    [Inject] private ToastService Toast { get; set; } = null!;

    [Parameter] public Guid? WorkflowId { get; set; }

    [CascadingParameter] private Vyshyvanka.Designer.Layout.DesignerLayout? Layout { get; set; }

    private RenderFragment _toolbarFragment => builder =>
    {
        builder.OpenComponent<Vyshyvanka.Designer.Layout.DesignerToolbar>(0);
        builder.AddAttribute(1, nameof(DesignerToolbar.IsHistoryCollapsed), _isHistoryCollapsed);
        builder.AddAttribute(2, nameof(DesignerToolbar.OnOpen), EventCallback.Factory.Create(this, OpenWorkflowBrowser));
        builder.AddAttribute(3, nameof(DesignerToolbar.OnSave), EventCallback.Factory.Create(this, SaveWorkflow));
        builder.AddAttribute(4, nameof(DesignerToolbar.OnExecute), EventCallback.Factory.Create(this, ExecuteWorkflow));
        builder.AddAttribute(5, nameof(DesignerToolbar.OnStop), EventCallback.Factory.Create(this, StopExecution));
        builder.AddAttribute(6, nameof(DesignerToolbar.OnOpenPlugins), EventCallback.Factory.Create(this, OpenPluginManager));
        builder.AddAttribute(7, nameof(DesignerToolbar.OnOpenCredentials), EventCallback.Factory.Create(this, OpenCredentialManager));
        builder.AddAttribute(8, nameof(DesignerToolbar.OnToggleHistory), EventCallback.Factory.Create(this, ToggleHistory));
        builder.CloseComponent();
    };

    private bool _isValidationPanelExpanded = true;
    private bool _isPluginManagerOpen;
    private bool _isCredentialManagerOpen;
    private bool _isWorkflowBrowserOpen;
    private bool _isNodeEditorOpen;
    private string? _editingNodeId;
    private CancellationTokenSource? _pollCts;
    private Guid? _loadedWorkflowId;
    private bool _isPaletteCollapsed;
    private bool _isConfigCollapsed;
    private bool _isHistoryCollapsed = true;
    private bool _isExecutionViewActive;
    private ExecutionHistoryPanel? _historyPanel;

    protected override async Task OnInitializedAsync()
    {
        Store.OnStateChanged += StateHasChanged;
        ExecutionState.OnExecutionChanged += OnExecutionChanged;

        // Load node definitions
        try
        {
            var definitions = await ApiClient.GetNodeDefinitionsAsync();
            Store.SetNodeDefinitions(definitions);
        }
        catch
        {
            // Use default definitions if API is not available
            Store.SetNodeDefinitions(GetDefaultNodeDefinitions());
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        // Load workflow if ID changed
        if (WorkflowId != _loadedWorkflowId)
        {
            _loadedWorkflowId = WorkflowId;

            if (WorkflowId.HasValue)
            {
                try
                {
                    var workflow = await ApiClient.GetWorkflowAsync(WorkflowId.Value);
                    if (workflow is not null)
                    {
                        EditService.LoadWorkflow(workflow);
                    }
                }
                catch
                {
                    // Start with new workflow if load fails
                    EditService.NewWorkflow();
                }
            }
            else
            {
                EditService.NewWorkflow();
            }

            // Refresh execution history for the new workflow
            if (_historyPanel is not null)
            {
                await _historyPanel.RefreshAsync();
            }
        }
    }

    private void OpenPluginManager() => _isPluginManagerOpen = true;
    private void ClosePluginManager() => _isPluginManagerOpen = false;

    private void OpenCredentialManager() => _isCredentialManagerOpen = true;
    private void CloseCredentialManager() => _isCredentialManagerOpen = false;

    private void OpenWorkflowBrowser() => _isWorkflowBrowserOpen = true;
    private void CloseWorkflowBrowser() => _isWorkflowBrowserOpen = false;

    private void OpenNodeEditor(string nodeId)
    {
        _editingNodeId = nodeId;
        _isNodeEditorOpen = true;
    }

    private void CloseNodeEditor()
    {
        _isNodeEditorOpen = false;
        _editingNodeId = null;
    }

    private void ToggleValidationPanel() => _isValidationPanelExpanded = !_isValidationPanelExpanded;

    private void TogglePalette() => _isPaletteCollapsed = !_isPaletteCollapsed;

    private void ToggleConfig() => _isConfigCollapsed = !_isConfigCollapsed;

    private void ToggleHistory() => _isHistoryCollapsed = !_isHistoryCollapsed;

    private void OnExecutionSelected(ExecutionResponse execution)
    {
        _isExecutionViewActive = true;
        StateHasChanged();
    }

    private void OnExecutionCleared()
    {
        _isExecutionViewActive = false;
        StateHasChanged();
    }

    private void CloseExecutionInspector()
    {
        _isExecutionViewActive = false;
    }


    private async Task SaveWorkflow()
    {
        if (!ValidationService.ValidationResult.IsValid)
            return;

        try
        {
            Workflow? saved;
            var isNew = !WorkflowId.HasValue;

            if (WorkflowId.HasValue)
            {
                saved = await ApiClient.UpdateWorkflowAsync(Store.Workflow);
            }
            else
            {
                saved = await ApiClient.CreateWorkflowAsync(Store.Workflow);
            }

            if (saved is not null)
            {
                // Update state with the saved workflow (has correct ID and version)
                EditService.LoadWorkflow(saved);
                Store.MarkAsSaved();

                Toast.ShowSuccess(
                    isNew
                        ? $"Workflow '{saved.Name}' created successfully"
                        : $"Workflow '{saved.Name}' updated (v{saved.Version})",
                    isNew ? "Created" : "Saved");

                // Navigate to the workflow URL if this was a new workflow
                if (isNew)
                {
                    _loadedWorkflowId = saved.Id;
                    Navigation.NavigateTo($"/designer/{saved.Id}", forceLoad: false);
                }
            }
        }
        catch (ApiException ex)
        {
            Toast.ShowError(ex.Error.GetFullMessage(), $"Save Failed ({ex.Error.Code})");
        }
        catch (Exception ex)
        {
            Toast.ShowError(ex.Message, "Save Failed");
        }
    }

    private async Task ExecuteWorkflow()
    {
        if (!ValidationService.ValidationResult.IsValid)
            return;

        try
        {
            var execution = await ApiClient.ExecuteWorkflowAsync(Store.Workflow.Id);
            if (execution is not null)
            {
                ExecutionState.SetCurrentExecution(execution);
                StartExecutionPolling(execution.Id);
                Toast.ShowInfo($"Execution started (ID: {execution.Id.ToString()[..8]}...)", "Running");
            }
        }
        catch (ApiException ex)
        {
            Toast.ShowError(ex.Error.GetFullMessage(), $"Execution Failed ({ex.Error.Code})");
        }
        catch (Exception ex)
        {
            Toast.ShowError(ex.Message, "Execution Failed");
        }
    }

    private void StopExecution()
    {
        StopExecutionPolling();
        ExecutionState.ClearExecutionState();
        Toast.ShowInfo("Execution stopped", "Stopped");
    }

    private void OnExecutionChanged(ExecutionResponse? execution)
    {
        if (execution is not null)
        {
            if (execution.Status == ExecutionStatus.Completed)
            {
                StopExecutionPolling();
                Toast.ShowSuccess($"Workflow executed successfully in {execution.Duration?.TotalMilliseconds:0}ms",
                    "Completed");
            }
            else if (execution.Status == ExecutionStatus.Failed)
            {
                StopExecutionPolling();
                Toast.ShowError(execution.ErrorMessage ?? "Execution failed", "Failed");
            }
            else if (execution.Status == ExecutionStatus.Cancelled)
            {
                StopExecutionPolling();
                Toast.ShowWarning("Execution was cancelled", "Cancelled");
            }
        }

        InvokeAsync(StateHasChanged);
    }

    private void StartExecutionPolling(Guid executionId)
    {
        StopExecutionPolling();
        _pollCts = new CancellationTokenSource();
        _ = PollLoopAsync(executionId, _pollCts.Token);
    }

    private void StopExecutionPolling()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
    }

    private async Task PollLoopAsync(Guid executionId, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await PollExecutionStatus(executionId);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when polling is stopped
        }
    }

    private async Task PollExecutionStatus(Guid executionId)
    {
        try
        {
            var execution = await ApiClient.GetExecutionAsync(executionId);
            if (execution is not null)
            {
                ExecutionState.UpdateExecution(execution);
            }
        }
        catch
        {
            // Ignore polling errors
        }
    }

    private static IEnumerable<NodeDefinition> GetDefaultNodeDefinitions()
    {
        return
        [
            new NodeDefinition
            {
                Type = "manual-trigger",
                Name = "Manual Trigger",
                Description = "Manually trigger workflow execution",
                Category = NodeCategory.Trigger,
                Icon = "fa-solid fa-play",
                Outputs = [new PortDefinition { Name = "output", DisplayName = "Output", Type = PortType.Any }]
            },
            new NodeDefinition
            {
                Type = "webhook-trigger",
                Name = "Webhook Trigger",
                Description = "Trigger workflow via HTTP webhook",
                Category = NodeCategory.Trigger,
                Icon = "fa-solid fa-tower-broadcast",
                Outputs = [new PortDefinition { Name = "output", DisplayName = "Output", Type = PortType.Object }]
            },
            new NodeDefinition
            {
                Type = "schedule-trigger",
                Name = "Schedule Trigger",
                Description = "Trigger workflow on a schedule",
                Category = NodeCategory.Trigger,
                Icon = "fa-solid fa-clock",
                Outputs = [new PortDefinition { Name = "output", DisplayName = "Output", Type = PortType.Object }]
            },
            new NodeDefinition
            {
                Type = "if",
                Name = "If",
                Description = "Conditional branching",
                Category = NodeCategory.Logic,
                Icon = "fa-solid fa-code-branch",
                Inputs =
                [
                    new PortDefinition { Name = "input", DisplayName = "Input", Type = PortType.Any, IsRequired = true }
                ],
                Outputs =
                [
                    new PortDefinition { Name = "true", DisplayName = "True", Type = PortType.Any },
                    new PortDefinition { Name = "false", DisplayName = "False", Type = PortType.Any }
                ]
            },
            new NodeDefinition
            {
                Type = "switch",
                Name = "Switch",
                Description = "Multi-way branching",
                Category = NodeCategory.Logic,
                Icon = "fa-solid fa-shuffle",
                Inputs =
                [
                    new PortDefinition { Name = "input", DisplayName = "Input", Type = PortType.Any, IsRequired = true }
                ],
                Outputs = [new PortDefinition { Name = "output", DisplayName = "Output", Type = PortType.Any }]
            },
            new NodeDefinition
            {
                Type = "merge",
                Name = "Merge",
                Description = "Merge multiple branches",
                Category = NodeCategory.Logic,
                Icon = "fa-solid fa-code-merge",
                Inputs =
                [
                    new PortDefinition { Name = "input1", DisplayName = "Input 1", Type = PortType.Any },
                    new PortDefinition { Name = "input2", DisplayName = "Input 2", Type = PortType.Any }
                ],
                Outputs = [new PortDefinition { Name = "output", DisplayName = "Output", Type = PortType.Any }]
            },
            new NodeDefinition
            {
                Type = "loop",
                Name = "Loop",
                Description = "Iterate over items",
                Category = NodeCategory.Logic,
                Icon = "fa-solid fa-rotate",
                Inputs =
                [
                    new PortDefinition
                        { Name = "input", DisplayName = "Input", Type = PortType.Array, IsRequired = true }
                ],
                Outputs =
                [
                    new PortDefinition { Name = "item", DisplayName = "Item", Type = PortType.Any },
                    new PortDefinition { Name = "done", DisplayName = "Done", Type = PortType.Array }
                ]
            }
        ];
    }

    public ValueTask DisposeAsync()
    {
        Store.OnStateChanged -= StateHasChanged;
        ExecutionState.OnExecutionChanged -= OnExecutionChanged;
        StopExecutionPolling();
        return ValueTask.CompletedTask;
    }
}
