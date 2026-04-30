using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Vyshyvanka.Designer.Components;

public partial class CanvasNodeComponent
{
    [Inject] private WorkflowStateService StateService { get; set; } = null!;

    [Parameter, EditorRequired] public WorkflowNode Node { get; set; } = null!;

    [Parameter] public NodeDefinition? Definition { get; set; }

    [Parameter] public bool IsSelected { get; set; }

    [Parameter] public EventCallback OnSelect { get; set; }

    [Parameter] public EventCallback<MouseEventArgs> OnStartDrag { get; set; }

    [Parameter] public EventCallback<string> OnStartConnection { get; set; }

    [Parameter] public EventCallback<string> OnEndConnection { get; set; }

    [Parameter] public EventCallback<Position> OnMove { get; set; }

    [Parameter] public EventCallback OnDelete { get; set; }

    /// <summary>
    /// Event callback invoked when the node is double-clicked to open the editor modal.
    /// </summary>
    [Parameter]
    public EventCallback OnDoubleClick { get; set; }

    private double ComputedWidth => NodeLayout.GetWidth(Node.Name);
    private double ComputedHeight => NodeLayout.GetHeight(Definition);

    private string GetCategoryClass() => Definition?.Category switch
    {
        NodeCategory.Trigger => "category-trigger",
        NodeCategory.Action => "category-action",
        NodeCategory.Logic => "category-logic",
        NodeCategory.Transform => "category-transform",
        _ => ""
    };

    private static string GetPortTypeClass(PortType type) => type switch
    {
        PortType.String => "port-string",
        PortType.Number => "port-number",
        PortType.Boolean => "port-boolean",
        PortType.Array => "port-array",
        PortType.Object => "port-object",
        _ => "port-any"
    };

    private string GetExecutionStatusClass()
    {
        var execState = StateService.GetNodeExecutionState(Node.Id);
        return execState?.Status switch
        {
            ExecutionStatus.Running => "executing",
            ExecutionStatus.Completed => "executed-success",
            ExecutionStatus.Failed => "executed-failed",
            ExecutionStatus.Pending => "execution-pending",
            _ => ""
        };
    }

    private static string GetExecutionIndicatorClass(ExecutionStatus status) => status switch
    {
        ExecutionStatus.Running => "indicator-running",
        ExecutionStatus.Completed => "indicator-completed",
        ExecutionStatus.Failed => "indicator-failed",
        ExecutionStatus.Pending => "indicator-pending",
        _ => ""
    };

    private static string GetStatusBadgeColor(ExecutionStatus status) => status switch
    {
        ExecutionStatus.Running => "#1976d2",
        ExecutionStatus.Completed => "#4caf50",
        ExecutionStatus.Failed => "#ef5350",
        ExecutionStatus.Pending => "#ff9800",
        _ => "#9e9e9e"
    };

    private static string GetStatusBadgeIcon(ExecutionStatus status) => status switch
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

    private async Task OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == 0)
        {
            await OnStartDrag.InvokeAsync(e);
        }
    }

    private void OnPortMouseDown(string portName)
    {
        OnStartConnection.InvokeAsync(portName);
    }

    private void OnPortMouseUp(string portName)
    {
        OnEndConnection.InvokeAsync(portName);
    }

    private async Task OnDeleteClick()
    {
        await OnDelete.InvokeAsync();
    }

    private async Task OnNodeDoubleClick()
    {
        await OnDoubleClick.InvokeAsync();
    }
}
