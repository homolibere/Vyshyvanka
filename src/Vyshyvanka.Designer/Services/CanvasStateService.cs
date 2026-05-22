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
        if (_pendingConnection is null) return;
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

    /// <summary>Resets the canvas view to zoom 1.0, centered on the first node (or origin if no nodes).</summary>
    public void ResetView()
    {
        var firstNode = store.Workflow.Nodes.FirstOrDefault();
        if (firstNode is not null)
        {
            var panX = -firstNode.Position.X + _canvasState.Width / 2;
            var panY = -firstNode.Position.Y + _canvasState.Height / 2;
            _canvasState = new CanvasState
            {
                PanX = panX,
                PanY = panY,
                Zoom = 1.0,
                Width = _canvasState.Width,
                Height = _canvasState.Height
            };
        }
        else
        {
            _canvasState = new CanvasState { Width = _canvasState.Width, Height = _canvasState.Height };
        }

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
            _selectedNodeId = null;
    }

    /// <summary>Clears the selected connection if it matches.</summary>
    internal void ClearSelectedConnectionIfMatches(Connection connection)
    {
        if (_selectedConnection == connection)
            _selectedConnection = null;
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
    }

    /// <summary>Undoes the last action.</summary>
    public void Undo()
    {
        if (!CanUndo) return;

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
        if (!CanRedo) return;

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
