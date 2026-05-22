using Vyshyvanka.Core.Enums;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Components;

public partial class NodeConfigPanel : IDisposable
{
    [Inject] private WorkflowStore Store { get; set; } = null!;

    [Inject] private WorkflowEditService EditService { get; set; } = null!;

    [Inject] private CanvasStateService CanvasState { get; set; } = null!;

    [Inject] private ExecutionStateService ExecutionState { get; set; } = null!;

    [Inject] private WorkflowApiClient ApiClient { get; set; } = null!;

    [Inject] private ToastService ToastService { get; set; } = null!;

    private string configJson = "{}";
    private string nodeName = "";
    private string? configError;
    private string _workflowName = "";
    private string _workflowDescription = "";
    private bool _isRunningToNode;

    protected override void OnInitialized()
    {
        Store.OnStateChanged += OnStateChanged;
        _workflowName = Store.Workflow.Name;
        _workflowDescription = Store.Workflow.Description ?? "";
    }

    private void OnStateChanged()
    {
        var node = CanvasState.GetSelectedNode();
        if (node is not null)
        {
            nodeName = node.Name;
            try
            {
                configJson = node.Configuration.GetRawText();
            }
            catch
            {
                configJson = "{}";
            }
        }
        else
        {
            // Update workflow properties when no node selected
            _workflowName = Store.Workflow.Name;
            _workflowDescription = Store.Workflow.Description ?? "";
        }

        configError = null;
        StateHasChanged();
    }

    private void UpdateWorkflowName()
    {
        if (string.IsNullOrWhiteSpace(_workflowName)) return;
        if (_workflowName != Store.Workflow.Name)
        {
            EditService.UpdateWorkflowMetadata(_workflowName, Store.Workflow.Description);
        }
    }

    private void UpdateWorkflowDescription()
    {
        var desc = string.IsNullOrWhiteSpace(_workflowDescription) ? null : _workflowDescription;
        if (desc != Store.Workflow.Description)
        {
            EditService.UpdateWorkflowMetadata(Store.Workflow.Name, desc);
        }
    }

    private void UpdateNodeName(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeName)) return;
        EditService.UpdateNodeName(nodeId, nodeName);
    }

    private void ApplyConfiguration()
    {
        var node = CanvasState.GetSelectedNode();
        if (node is null) return;

        try
        {
            var config = System.Text.Json.JsonDocument.Parse(configJson).RootElement;
            EditService.UpdateNodeConfiguration(node.Id, config);
            configError = null;
        }
        catch (System.Text.Json.JsonException ex)
        {
            configError = $"Invalid JSON: {ex.Message}";
        }
    }

    private void DeleteNode()
    {
        if (CanvasState.SelectedNodeId is not null)
        {
            EditService.RemoveNode(CanvasState.SelectedNodeId);
        }
    }

    private static string GetPortTypeClass(PortType type) => type switch
    {
        PortType.String => "port-string",
        PortType.Number => "port-number",
        PortType.Boolean => "port-boolean",
        PortType.Array => "port-array",
        PortType.Object => "port-object",
        _ => "port-any"
    };

    private static string GetExecutionStatusClass(ExecutionStatus status) => status switch
    {
        ExecutionStatus.Running => "status-running",
        ExecutionStatus.Completed => "status-completed",
        ExecutionStatus.Failed => "status-failed",
        ExecutionStatus.Pending => "status-pending",
        _ => ""
    };

    private static string GetStatusIcon(ExecutionStatus status) => status switch
    {
        ExecutionStatus.Running => "⟳",
        ExecutionStatus.Completed => "✓",
        ExecutionStatus.Failed => "✗",
        ExecutionStatus.Pending => "⏳",
        _ => "○"
    };

    private static string FormatDuration(double ms)
    {
        if (ms < 1000)
            return $"{ms:F0}ms";
        if (ms < 60000)
            return $"{ms / 1000:F1}s";
        return $"{ms / 60000:F1}m";
    }

    private static string FormatJson(System.Text.Json.JsonElement element)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Serialize(element, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch
        {
            return element.GetRawText();
        }
    }

    private async Task RunUpToNode()
    {
        var nodeId = CanvasState.SelectedNodeId;
        if (nodeId is null || _isRunningToNode) return;

        _isRunningToNode = true;
        StateHasChanged();

        try
        {
            // Save the workflow to the API so the execution uses the latest state
            if (Store.IsDirty)
            {
                var saved = await ApiClient.UpdateWorkflowAsync(Store.Workflow);
                if (saved is not null)
                {
                    EditService.LoadWorkflow(saved);
                    Store.MarkAsSaved();
                }
            }

            var result = await ApiClient.ExecuteUpToNodeAsync(
                Store.Workflow.Id, nodeId);

            if (result is not null)
            {
                ExecutionState.SetCurrentExecution(result);

                if (result.Status == ExecutionStatus.Failed)
                {
                    ToastService.ShowError(result.ErrorMessage ?? "Execution failed");
                }
                else
                {
                    ToastService.ShowSuccess($"Executed up to node — {result.NodeExecutions.Count} node(s) ran");
                }
            }
        }
        catch (ApiException ex)
        {
            ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Execution failed: {ex.Message}");
        }
        finally
        {
            _isRunningToNode = false;
            StateHasChanged();
        }
    }

    public void Dispose()
    {
        Store.OnStateChanged -= OnStateChanged;
    }
}
