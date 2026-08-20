using Vyshyvanka.Core.Models;
using Vyshyvanka.Designer.Models;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// Manages canvas interaction state: pan, zoom, selection, undo/redo,
/// pending connections, and drag-from-palette state.
/// </summary>
public class CanvasStateService(WorkflowStore store)
{
    private CanvasState _canvasState = new();
    private string? _selectedNodeId;
    private Connection? _selectedConnection;
    private PendingConnection? _pendingConnection;
    private string? _draggedNodeType;
    /// <summary>Maximum number of undo states retained in memory.</summary>
    internal const int MaxUndoHistory = 50;

    private readonly Stack<CanvasAction> _undoStack = new();
    private readonly Stack<CanvasAction> _redoStack = new();

    /// <summary>Gets the current canvas state.</summary>
    public CanvasState CanvasState => _canvasState;

    /// <summary>Gets the currently selected node ID.</summary>
    public string? SelectedNodeId => _selectedNodeId;

    /// <summary>Gets the currently selected connection.</summary>
    public Connection? SelectedConnection => _selectedConnection;

    /// <summary>Gets the pending connection being drawn.</summary>
    public PendingConnection? PendingConnection => _pendingConnection;

    /// <summary>Gets the node type being dragged from the palette.</summary>
    public string? DraggedNodeType => _draggedNodeType;

    /// <summary>Gets whether undo is available.</summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>Gets whether redo is available.</summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Gets the selected node from the workflow.</summary>
    public WorkflowNode? GetSelectedNode()
    {
        return _selectedNodeId is not null ? store.GetNode(_selectedNodeId) : null;
    }

    /// <summary>Selects a node.</summary>
    public void SelectNode(string? nodeId)
    {
        _selectedNodeId = nodeId;
        _selectedConnection = null;
        store.NotifyStateChanged();
    }

    /// <summary>Selects a connection.</summary>
    public void SelectConnection(Connection? connection)
    {
        _selectedConnection = connection;
        _selectedNodeId = null;
        store.NotifyStateChanged();
    }

    /// <summary>Clears selection.</summary>
    public void ClearSelection()
    {
        _selectedNodeId = null;
        _selectedConnection = null;
        store.NotifyStateChanged();
    }

    /// <summary>Starts drawing a connection from a port.</summary>
    public void StartConnection(string sourceNodeId, string sourcePort, double x, double y)
    {
        _pendingConnection = new PendingConnection
        {
            SourceNodeId = sourceNodeId,
            SourcePort = sourcePort,
            CurrentX = x,
            CurrentY = y
        };
        store.NotifyStateChanged();
    }

    /// <summary>Updates the pending connection position.</summary>
    public void UpdatePendingConnection(double x, double y)
    {
        if (_pendingConnection is null)
        {
            return;
        }

        _pendingConnection = _pendingConnection with { CurrentX = x, CurrentY = y };
        store.NotifyStateChanged();
    }

    /// <summary>Cancels the pending connection (without completing it).</summary>
    public void CancelPendingConnection()
    {
        _pendingConnection = null;
        store.NotifyStateChanged();
    }

    /// <summary>Gets and clears the pending connection (used by edit service to complete it).</summary>
    internal PendingConnection? ConsumePendingConnection()
    {
        var pending = _pendingConnection;
        _pendingConnection = null;
        return pending;
    }

    /// <summary>Updates the canvas pan position.</summary>
    public void Pan(double deltaX, double deltaY)
    {
        _canvasState = _canvasState with
        {
            PanX = _canvasState.PanX + deltaX,
            PanY = _canvasState.PanY + deltaY
        };
        store.NotifyStateChanged();
    }

    /// <summary>Updates the canvas zoom level.</summary>
    public void Zoom(double zoom, double? centerX = null, double? centerY = null)
    {
        var newZoom = Math.Clamp(zoom, 0.25, 2.0);
        _canvasState = _canvasState with { Zoom = newZoom };
        store.NotifyStateChanged();
    }

    /// <summary>Fits all nodes into the viewport with appropriate zoom and centering.</summary>
    public void ResetView()
    {
        var nodes = store.Workflow.Nodes;
        if (nodes.Count == 0)
        {
            _canvasState = new CanvasState { Width = _canvasState.Width, Height = _canvasState.Height };
            store.NotifyStateChanged();
            return;
        }

        // Node dimensions (matching CSS --node-width: 220px, --node-min-height: 80px)
        const double nodeWidth = 220;
        const double nodeHeight = 80;
        const double padding = 60; // padding around the bounding box

        // Calculate bounding box of all nodes
        var minX = nodes.Min(n => n.Position.X);
        var minY = nodes.Min(n => n.Position.Y);
        var maxX = nodes.Max(n => n.Position.X + nodeWidth);
        var maxY = nodes.Max(n => n.Position.Y + nodeHeight);

        var contentWidth = maxX - minX + padding * 2;
        var contentHeight = maxY - minY + padding * 2;

        var viewWidth = _canvasState.Width;
        var viewHeight = _canvasState.Height;

        if (viewWidth <= 0 || viewHeight <= 0)
        {
            // Canvas size not yet known, fall back to centering first node
            var first = nodes[0];
            _canvasState = new CanvasState
            {
                PanX = -first.Position.X + 400,
                PanY = -first.Position.Y + 300,
                Zoom = 1.0,
                Width = _canvasState.Width,
                Height = _canvasState.Height
            };
            store.NotifyStateChanged();
            return;
        }

        // Calculate zoom to fit all content, clamped between 0.25 and 1.0
        var zoomX = viewWidth / contentWidth;
        var zoomY = viewHeight / contentHeight;
        var zoom = Math.Clamp(Math.Min(zoomX, zoomY), 0.25, 1.0);

        // Center the bounding box in the viewport
        var centerX = (minX + maxX) / 2;
        var centerY = (minY + maxY) / 2;
        var panX = viewWidth / 2 / zoom - centerX;
        var panY = viewHeight / 2 / zoom - centerY;

        _canvasState = new CanvasState
        {
            PanX = panX,
            PanY = panY,
            Zoom = zoom,
            Width = viewWidth,
            Height = viewHeight
        };

        store.NotifyStateChanged();
    }

    /// <summary>Updates the canvas size.</summary>
    public void SetCanvasSize(double width, double height)
    {
        _canvasState = _canvasState with { Width = width, Height = height };
        store.NotifyStateChanged();
    }

    /// <summary>Resets the canvas state (used when loading/creating workflows).</summary>
    internal void ResetCanvasState()
    {
        _canvasState = new();
    }

    /// <summary>Clears selection state (used when loading/creating workflows).</summary>
    internal void ClearSelectionState()
    {
        _selectedNodeId = null;
        _selectedConnection = null;
        _pendingConnection = null;
    }

    /// <summary>Clears the selected node if it matches the given ID.</summary>
    internal void ClearSelectedNodeIfMatches(string nodeId)
    {
        if (_selectedNodeId == nodeId)
        {
            _selectedNodeId = null;
        }
    }

    /// <summary>Clears the selected connection if it matches.</summary>
    internal void ClearSelectedConnectionIfMatches(Connection connection)
    {
        if (_selectedConnection == connection)
        {
            _selectedConnection = null;
        }
    }

    /// <summary>Sets the selected node ID directly (used by edit service after adding a node).</summary>
    internal void SetSelectedNodeId(string? nodeId)
    {
        _selectedNodeId = nodeId;
    }

    /// <summary>Saves the current workflow state for undo.</summary>
    public void SaveUndoState(string description)
    {
        _undoStack.Push(new CanvasAction
        {
            Type = CanvasActionType.BatchOperation,
            Description = description,
            PreviousState = store.Workflow
        });
        _redoStack.Clear();
        TrimStack(_undoStack, MaxUndoHistory);
    }

    /// <summary>Discards the oldest entries when the stack exceeds the limit.</summary>
    private static void TrimStack(Stack<CanvasAction> stack, int maxSize)
    {
        if (stack.Count <= maxSize)
        {
            return;
        }

        var keep = stack.ToArray().AsSpan(0, maxSize); // index 0 = top (newest)
        stack.Clear();
        for (var i = keep.Length - 1; i >= 0; i--)
        {
            stack.Push(keep[i]);
        }
    }

    /// <summary>Undoes the last action.</summary>
    public void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        using var _ = store.SuspendNotifications();
        var action = _undoStack.Pop();
        if (action.PreviousState is not null)
        {
            _redoStack.Push(new CanvasAction
            {
                Type = action.Type,
                Description = action.Description,
                PreviousState = store.Workflow,
                NewState = action.PreviousState
            });
            store.SetWorkflow(action.PreviousState);
            store.NotifyStateChanged();
        }
    }

    /// <summary>Redoes the last undone action.</summary>
    public void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        using var _ = store.SuspendNotifications();
        var action = _redoStack.Pop();
        if (action.PreviousState is not null)
        {
            _undoStack.Push(new CanvasAction
            {
                Type = action.Type,
                Description = action.Description,
                PreviousState = store.Workflow,
                NewState = action.PreviousState
            });
            store.SetWorkflow(action.PreviousState);
            store.NotifyStateChanged();
        }
    }

    /// <summary>Starts dragging a node from the palette.</summary>
    public void StartDragFromPalette(string nodeType)
    {
        _draggedNodeType = nodeType;
    }

    /// <summary>Ends dragging a node from the palette.</summary>
    public void EndDragFromPalette()
    {
        _draggedNodeType = null;
    }
}
