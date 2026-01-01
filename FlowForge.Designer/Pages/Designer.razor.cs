using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using FlowForge.Designer.Models;
using FlowForge.Designer.Services;
using Microsoft.AspNetCore.Components;

namespace FlowForge.Designer.Pages;

public partial class Designer : IDisposable
{
    [Inject]
    private WorkflowStateService StateService { get; set; } = null!;

    [Inject]
    private FlowForgeApiClient ApiClient { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private ToastService Toast { get; set; } = null!;

    [Parameter]
    public Guid? WorkflowId { get; set; }

    private bool _isValidationPanelExpanded = true;
    private bool _isPluginManagerOpen;
    private bool _isWorkflowBrowserOpen;
    private bool _isNodeEditorOpen;
    private string? _editingNodeId;
    private System.Timers.Timer? _executionPollTimer;
    private Guid? _loadedWorkflowId;

    protected override async Task OnInitializedAsync()
    {
        StateService.OnStateChanged += StateHasChanged;
        StateService.OnExecutionChanged += OnExecutionChanged;

        // Load node definitions
        try
        {
            var definitions = await ApiClient.GetNodeDefinitionsAsync();
            StateService.SetNodeDefinitions(definitions);
        }
        catch
        {
            // Use default definitions if API is not available
            StateService.SetNodeDefinitions(GetDefaultNodeDefinitions());
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
                        StateService.LoadWorkflow(workflow);
                    }
                }
                catch
                {
                    // Start with new workflow if load fails
                    StateService.NewWorkflow();
                }
            }
            else
            {
                StateService.NewWorkflow();
            }
        }
    }

    private void ZoomIn() => StateService.Zoom(StateService.CanvasState.Zoom + 0.1);
    private void ZoomOut() => StateService.Zoom(StateService.CanvasState.Zoom - 0.1);

    private void OpenPluginManager() => _isPluginManagerOpen = true;
    private void ClosePluginManager() => _isPluginManagerOpen = false;

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

    private string GetSaveButtonTitle() => StateService.ValidationResult.IsValid
        ? "Save workflow"
        : "Fix validation errors before saving";

    private string GetRunButtonTitle()
    {
        if (!StateService.ValidationResult.IsValid)
            return "Fix validation errors before running";
        if (!StateService.Workflow.IsActive)
            return "Activate workflow before running";
        return "Execute workflow";
    }

    private void ToggleWorkflowActive()
    {
        StateService.ToggleWorkflowActive();
    }

    private async Task SaveWorkflow()
    {
        if (!StateService.ValidationResult.IsValid)
            return;

        try
        {
            Workflow? saved;
            var isNew = !WorkflowId.HasValue;

            if (WorkflowId.HasValue)
            {
                saved = await ApiClient.UpdateWorkflowAsync(StateService.Workflow);
            }
            else
            {
                saved = await ApiClient.CreateWorkflowAsync(StateService.Workflow);
            }

            if (saved is not null)
            {
                // Update state with the saved workflow (has correct ID and version)
                StateService.LoadWorkflow(saved);
                StateService.MarkAsSaved();

                Toast.ShowSuccess(
                    isNew ? $"Workflow '{saved.Name}' created successfully" : $"Workflow '{saved.Name}' updated (v{saved.Version})",
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
        if (!StateService.ValidationResult.IsValid)
            return;

        try
        {
            var execution = await ApiClient.ExecuteWorkflowAsync(StateService.Workflow.Id);
            if (execution is not null)
            {
                StateService.SetCurrentExecution(execution);
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
        StateService.ClearExecutionState();
        Toast.ShowInfo("Execution stopped", "Stopped");
    }

    private void OnExecutionChanged(ExecutionResponse? execution)
    {
        if (execution is not null)
        {
            if (execution.Status == ExecutionStatus.Completed)
            {
                StopExecutionPolling();
                Toast.ShowSuccess($"Workflow executed successfully in {execution.Duration?.TotalMilliseconds:0}ms", "Completed");
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
        _executionPollTimer = new System.Timers.Timer(1000);
        _executionPollTimer.Elapsed += async (_, _) => await PollExecutionStatus(executionId);
        _executionPollTimer.Start();
    }

    private void StopExecutionPolling()
    {
        _executionPollTimer?.Stop();
        _executionPollTimer?.Dispose();
        _executionPollTimer = null;
    }

    private async Task PollExecutionStatus(Guid executionId)
    {
        try
        {
            var execution = await ApiClient.GetExecutionAsync(executionId);
            if (execution is not null)
            {
                StateService.UpdateExecution(execution);
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
                Inputs = [new PortDefinition { Name = "input", DisplayName = "Input", Type = PortType.Any, IsRequired = true }],
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
                Inputs = [new PortDefinition { Name = "input", DisplayName = "Input", Type = PortType.Any, IsRequired = true }],
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
                Inputs = [new PortDefinition { Name = "input", DisplayName = "Input", Type = PortType.Array, IsRequired = true }],
                Outputs =
                [
                    new PortDefinition { Name = "item", DisplayName = "Item", Type = PortType.Any },
                    new PortDefinition { Name = "done", DisplayName = "Done", Type = PortType.Array }
                ]
            }
        ];
    }

    public void Dispose()
    {
        StateService.OnStateChanged -= StateHasChanged;
        StateService.OnExecutionChanged -= OnExecutionChanged;
        StopExecutionPolling();
    }
}
