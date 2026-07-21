using Vyshyvanka.Core.Models;
using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Vyshyvanka.Designer.Components;

public partial class WorkflowCanvas : IAsyncDisposable
{
    [Inject] private WorkflowStore Store { get; set; } = null!;

    [Inject] private CanvasStateService CanvasState { get; set; } = null!;

    [Inject] private WorkflowEditService EditService { get; set; } = null!;

    [Inject] private IJSRuntime JS { get; set; } = null!;

    [Inject] private ThemeService ThemeService { get; set; } = null!;

    /// <summary>
    /// Event callback invoked when a node is double-clicked to open the editor modal.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnNodeDoubleClick { get; set; }

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
        Store.OnStateChanged += StateHasChanged;
        ThemeService.OnThemeChanged += StateHasChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);

            // Get initial dimensions
            var dimensions =
                await JS.InvokeAsync<CanvasDimensions>("canvasInterop.getElementDimensions", _canvasContainer);
            CanvasState.SetCanvasSize(dimensions.Width, dimensions.Height);

            // Set up resize observer
            _resizeObserver =
                await JS.InvokeAsync<IJSObjectReference>("canvasInterop.observeResize", _canvasContainer, _dotNetRef);
        }
    }

    /// <summary>Called from JavaScript when the canvas container is resized.</summary>
    [JSInvokable]
    public void OnCanvasResized(double width, double height)
    {
        CanvasState.SetCanvasSize(width, height);
    }

    private string GetViewBox()
    {
        var state = CanvasState.CanvasState;
        var width = state.Width / state.Zoom;
        var height = state.Height / state.Zoom;
        var x = -state.PanX / state.Zoom;
        var y = -state.PanY / state.Zoom;
        return FormattableString.Invariant($"{x} {y} {width} {height}");
    }


    private void OnCanvasMouseDown(MouseEventArgs e)
    {
        // Don't start canvas drag if we're drawing a connection
        if (CanvasState.PendingConnection is not null)
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
        if (CanvasState.PendingConnection is not null)
        {
            var state = CanvasState.CanvasState;
            var x = (e.OffsetX - state.PanX) / state.Zoom;
            var y = (e.OffsetY - state.PanY) / state.Zoom;
            CanvasState.UpdatePendingConnection(x, y);
        }
        else if (isPanning)
        {
            var deltaX = e.ClientX - lastMouseX;
            var deltaY = e.ClientY - lastMouseY;
            CanvasState.Pan(deltaX, deltaY);
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
            var state = CanvasState.CanvasState;
            var deltaX = (e.ClientX - lastMouseX) / state.Zoom;
            var deltaY = (e.ClientY - lastMouseY) / state.Zoom;

            var node = Store.GetNode(draggingNodeId);
            if (node is not null)
            {
                EditService.MoveNode(draggingNodeId, node.Position.X + deltaX, node.Position.Y + deltaY);
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
            CanvasState.ClearSelection();
        }

        isPanning = false;
        isCanvasDragStarted = false;
        draggingNodeId = null;
        if (CanvasState.PendingConnection is not null)
        {
            EditService.EndConnection();
        }
    }

    private void OnCanvasWheel(WheelEventArgs e)
    {
        // Use a smaller factor for smoother zooming; scale by actual delta magnitude
        // Typical deltaY is ~100 for a single wheel tick, trackpads produce smaller values
        var zoomSensitivity = 0.001;
        var delta = -e.DeltaY * zoomSensitivity;

        // Clamp individual zoom step to avoid jumps from high-velocity scroll
        delta = Math.Clamp(delta, -0.05, 0.05);

        CanvasState.Zoom(CanvasState.CanvasState.Zoom + delta);
    }

    private static void OnDragOver(DragEventArgs e)
    {
        e.DataTransfer.DropEffect = "copy";
    }

    private void OnDrop(DragEventArgs e)
    {
        if (CanvasState.DraggedNodeType is null) return;

        var state = CanvasState.CanvasState;
        var x = (e.OffsetX - state.PanX) / state.Zoom;
        var y = (e.OffsetY - state.PanY) / state.Zoom;

        EditService.DropNodeFromPalette(x, y);
    }

    private void StartNodeDrag(string nodeId, MouseEventArgs e)
    {
        draggingNodeId = nodeId;
        lastMouseX = e.ClientX;
        lastMouseY = e.ClientY;
        CanvasState.SelectNode(nodeId);
    }

    private async Task HandleNodeDoubleClick(string nodeId)
    {
        await OnNodeDoubleClick.InvokeAsync(nodeId);
    }

    private void StartConnection(string nodeId, string port)
    {
        var pos = GetPortPosition(nodeId, port, isOutput: true);
        CanvasState.StartConnection(nodeId, port, pos.X, pos.Y);
    }

    private void EndConnection(string nodeId, string port)
    {
        EditService.EndConnection(nodeId, port);
    }

    private (double X, double Y) GetPortPosition(string nodeId, string portName, bool isOutput)
    {
        var node = Store.GetNode(nodeId);
        if (node is null) return (0, 0);

        var definition = Store.GetNodeDefinition(node.Type);
        var nodeWidth = NodeLayout.GetWidth(node.Name);

        // Resolve effective ports (accounts for dynamic ports like Switch cases)
        var effectiveOutputs = NodeLayout.GetEffectiveOutputs(node, definition);
        var inputCount = definition?.Inputs?.Count ?? 0;
        var outputCount = effectiveOutputs.Count;
        var nodeHeight = NodeLayout.GetHeight(inputCount, outputCount);

        var x = isOutput ? node.Position.X + nodeWidth : node.Position.X;
        var y = node.Position.Y + nodeHeight / 2;

        // Adjust Y based on port index
        if (isOutput)
        {
            var index = effectiveOutputs.FindIndex(p => p.Name == portName);
            if (index >= 0)
            {
                var bodyHeight = nodeHeight - NodeLayout.HeaderHeight;
                var startY = node.Position.Y + NodeLayout.HeaderHeight +
                             (bodyHeight - (outputCount - 1) * NodeLayout.PortSpacing) / 2;
                y = startY + index * NodeLayout.PortSpacing;
            }
        }
        else
        {
            var ports = definition?.Inputs;
            if (ports is not null)
            {
                var index = ports.FindIndex(p => p.Name == portName);
                if (index >= 0)
                {
                    var totalPorts = ports.Count;
                    var bodyHeight = nodeHeight - NodeLayout.HeaderHeight;
                    var startY = node.Position.Y + NodeLayout.HeaderHeight +
                                 (bodyHeight - (totalPorts - 1) * NodeLayout.PortSpacing) / 2;
                    y = startY + index * NodeLayout.PortSpacing;
                }
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
        return FormattableString.Invariant($"M {x1} {y1} C {cp1X} {y1}, {cp2X} {y2}, {x2} {y2}");
    }

    public async ValueTask DisposeAsync()
    {
        Store.OnStateChanged -= StateHasChanged;
        ThemeService.OnThemeChanged -= StateHasChanged;

        if (_resizeObserver is not null)
        {
            await JS.InvokeVoidAsync("canvasInterop.disconnectObserver", _resizeObserver);
            await _resizeObserver.DisposeAsync();
        }

        _dotNetRef?.Dispose();
    }

    internal record CanvasDimensions(double Width, double Height);
}
