using FlowForge.Core.Enums;
using FlowForge.Designer.Services;
using Microsoft.AspNetCore.Components;

namespace FlowForge.Designer.Components;

public partial class NodeConfigPanel : IDisposable
{
    [Inject]
    private WorkflowStateService StateService { get; set; } = null!;

    private string configJson = "{}";
    private string nodeName = "";
    private string? configError;
    private string _workflowName = "";
    private string _workflowDescription = "";

    protected override void OnInitialized()
    {
        StateService.OnStateChanged += OnStateChanged;
        _workflowName = StateService.Workflow.Name;
        _workflowDescription = StateService.Workflow.Description ?? "";
    }

    private void OnStateChanged()
    {
        var node = StateService.GetSelectedNode();
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
            _workflowName = StateService.Workflow.Name;
            _workflowDescription = StateService.Workflow.Description ?? "";
        }

        configError = null;
        StateHasChanged();
    }

    private void UpdateWorkflowName()
    {
        if (string.IsNullOrWhiteSpace(_workflowName)) return;
        if (_workflowName != StateService.Workflow.Name)
        {
            StateService.UpdateWorkflowMetadata(_workflowName, StateService.Workflow.Description);
        }
    }

    private void UpdateWorkflowDescription()
    {
        var desc = string.IsNullOrWhiteSpace(_workflowDescription) ? null : _workflowDescription;
        if (desc != StateService.Workflow.Description)
        {
            StateService.UpdateWorkflowMetadata(StateService.Workflow.Name, desc);
        }
    }

    private void UpdateNodeName(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeName)) return;
        StateService.UpdateNodeName(nodeId, nodeName);
    }

    private void ApplyConfiguration()
    {
        var node = StateService.GetSelectedNode();
        if (node is null) return;

        try
        {
            var config = System.Text.Json.JsonDocument.Parse(configJson).RootElement;
            StateService.UpdateNodeConfiguration(node.Id, config);
            configError = null;
        }
        catch (System.Text.Json.JsonException ex)
        {
            configError = $"Invalid JSON: {ex.Message}";
        }
    }

    private void DeleteNode()
    {
        if (StateService.SelectedNodeId is not null)
        {
            StateService.RemoveNode(StateService.SelectedNodeId);
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

    public void Dispose()
    {
        StateService.OnStateChanged -= OnStateChanged;
    }
}
