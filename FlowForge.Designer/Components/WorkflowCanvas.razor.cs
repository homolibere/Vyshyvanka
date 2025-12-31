using FlowForge.Core.Models;
using FlowForge.Designer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace FlowForge.Designer.Components;

public partial class WorkflowCanvas : IAsyncDisposable
{
    [Inject]
    private WorkflowStateService StateService { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    private ElementReference _canvasContainer;
    private IJSObjectReference? _resizeObserver;
    private DotNetObjectReference<WorkflowCanvas>? _dotNetRef;
    private bool isPanning;
    private bool isCanvasDragStarted;
    private string? draggingNodeId;
    private double lastMouseX;
    private double lastMouseY;
    private double dragStartX;
    private double dragStartY;
    private const double DragThreshold = 5;

    protected override void OnInitialized()
    {
        StateService.OnStateChanged += StateHasChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);

            // Get initial dimensions
            var dimensions = await JS.InvokeAsync<CanvasDimensions>("canvasInterop.getElementDimensions", _canvasContainer);
            StateService.SetCanvasSize(dimensions.Width, dimensions.Height);

            // Set up resize observer
            _resizeObserver = await JS.InvokeAsync<IJSObjectReference>("canvasInterop.observeResize", _canvasContainer, _dotNetRef);
        }
    }

    /// <summary>Called from JavaScript when the canvas container is resized.</summary>
    [JSInvokable]
    public void OnCanvasResized(double width, double height)
    {
        StateService.SetCanvasSize(width, height);
    }

    private string GetViewBox()
    {
        var state = StateService.CanvasState;
        var width = state.Width / state.Zoom;
        var height = state.Height / state.Zoom;
        var x = -state.PanX / state.Zoom;
        var y = -state.PanY / state.Zoom;
        return $"{x} {y} {width} {height}";
    }


    private void OnCanvasMouseDown(MouseEventArgs e)
    {
        // Don't start canvas drag if we're drawing a connection
        if (StateService.PendingConnection is not null)
            return;

        if (e.Button == 1 || (e.Button == 0 && e.ShiftKey)) // Middle click or Shift+Left click - immediate pan
        {
            isPanning = true;
            lastMouseX = e.ClientX;
            lastMouseY = e.ClientY;
        }
        else if (e.Button == 0)
        {
            // Left click on empty canvas - prepare for potential pan
            isCanvasDragStarted = true;
            dragStartX = e.ClientX;
            dragStartY = e.ClientY;
            lastMouseX = e.ClientX;
            lastMouseY = e.ClientY;
        }
    }

    private void OnCanvasMouseMove(MouseEventArgs e)
    {
        // Pending connection takes priority - update the line following cursor
        if (StateService.PendingConnection is not null)
        {
            var state = StateService.CanvasState;
            var x = (e.OffsetX - state.PanX) / state.Zoom;
            var y = (e.OffsetY - state.PanY) / state.Zoom;
            StateService.UpdatePendingConnection(x, y);
        }
        else if (isPanning)
        {
            var deltaX = e.ClientX - lastMouseX;
            var deltaY = e.ClientY - lastMouseY;
            StateService.Pan(deltaX, deltaY);
            lastMouseX = e.ClientX;
            lastMouseY = e.ClientY;
        }
        else if (isCanvasDragStarted)
        {
            // Check if we've moved enough to start panning
            var distX = Math.Abs(e.ClientX - dragStartX);
            var distY = Math.Abs(e.ClientY - dragStartY);
            if (distX > DragThreshold || distY > DragThreshold)
            {
                isPanning = true;
                isCanvasDragStarted = false;
            }
        }
        else if (draggingNodeId is not null)
        {
            // Move the dragged node
            var state = StateService.CanvasState;
            var deltaX = (e.ClientX - lastMouseX) / state.Zoom;
            var deltaY = (e.ClientY - lastMouseY) / state.Zoom;

            var node = StateService.GetNode(draggingNodeId);
            if (node is not null)
            {
                StateService.MoveNode(draggingNodeId, node.Position.X + deltaX, node.Position.Y + deltaY);
            }

            lastMouseX = e.ClientX;
            lastMouseY = e.ClientY;
        }
    }

    private void OnCanvasMouseUp(MouseEventArgs e)
    {
        // If we started a canvas drag but didn't pan, it was a click - clear selection
        if (isCanvasDragStarted && !isPanning)
        {
            StateService.ClearSelection();
        }

        isPanning = false;
        isCanvasDragStarted = false;
        draggingNodeId = null;
        if (StateService.PendingConnection is not null)
        {
            StateService.EndConnection();
        }
    }

    private void OnCanvasWheel(WheelEventArgs e)
    {
        var delta = e.DeltaY > 0 ? -0.1 : 0.1;
        StateService.Zoom(StateService.CanvasState.Zoom + delta);
    }

    private static void OnDragOver(DragEventArgs e)
    {
        e.DataTransfer.DropEffect = "copy";
    }

    private void OnDrop(DragEventArgs e)
    {
        if (StateService.DraggedNodeType is null) return;

        var state = StateService.CanvasState;
        var x = (e.OffsetX - state.PanX) / state.Zoom;
        var y = (e.OffsetY - state.PanY) / state.Zoom;

        StateService.DropNodeFromPalette(x, y);
    }

    private void StartNodeDrag(string nodeId, MouseEventArgs e)
    {
        draggingNodeId = nodeId;
        lastMouseX = e.ClientX;
        lastMouseY = e.ClientY;
        StateService.SelectNode(nodeId);
    }

    private void StartConnection(string nodeId, string port)
    {
        var pos = GetPortPosition(nodeId, port, isOutput: true);
        StateService.StartConnection(nodeId, port, pos.X, pos.Y);
    }

    private void EndConnection(string nodeId, string port)
    {
        StateService.EndConnection(nodeId, port);
    }

    private (double X, double Y) GetPortPosition(string nodeId, string portName, bool isOutput)
    {
        var node = StateService.GetNode(nodeId);
        if (node is null) return (0, 0);

        var definition = StateService.GetNodeDefinition(node.Type);
        const double nodeWidth = 180;
        const double nodeHeight = 80;
        const double portSpacing = 20;

        var x = isOutput ? node.Position.X + nodeWidth : node.Position.X;
        var y = node.Position.Y + nodeHeight / 2;

        // Adjust Y based on port index
        var ports = isOutput ? definition?.Outputs : definition?.Inputs;
        if (ports is not null)
        {
            var index = ports.FindIndex(p => p.Name == portName);
            if (index >= 0)
            {
                var totalPorts = ports.Count;
                var startY = node.Position.Y + (nodeHeight - (totalPorts - 1) * portSpacing) / 2;
                y = startY + index * portSpacing;
            }
        }

        return (x, y);
    }

    private (double StartX, double StartY, double EndX, double EndY) GetConnectionCoordinates(Connection connection)
    {
        var start = GetPortPosition(connection.SourceNodeId, connection.SourcePort, isOutput: true);
        var end = GetPortPosition(connection.TargetNodeId, connection.TargetPort, isOutput: false);
        return (start.X, start.Y, end.X, end.Y);
    }

    private static string GetBezierPath(double x1, double y1, double x2, double y2)
    {
        var dx = Math.Abs(x2 - x1) * 0.5;
        var cp1X = x1 + dx;
        var cp2X = x2 - dx;
        return $"M {x1} {y1} C {cp1X} {y1}, {cp2X} {y2}, {x2} {y2}";
    }

    public async ValueTask DisposeAsync()
    {
        StateService.OnStateChanged -= StateHasChanged;

        if (_resizeObserver is not null)
        {
            await JS.InvokeVoidAsync("canvasInterop.disconnectObserver", _resizeObserver);
            await _resizeObserver.DisposeAsync();
        }

        _dotNetRef?.Dispose();
    }

    private record CanvasDimensions(double Width, double Height);
}
