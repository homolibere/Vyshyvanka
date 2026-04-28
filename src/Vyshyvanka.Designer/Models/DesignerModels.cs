using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using System.Text.Json;

namespace Vyshyvanka.Designer.Models;

/// <summary>
/// Represents the current state of the workflow canvas.
/// </summary>
public record CanvasState
{
    /// <summary>Current pan offset X.</summary>
    public double PanX { get; init; }

    /// <summary>Current pan offset Y.</summary>
    public double PanY { get; init; }

    /// <summary>Current zoom level (1.0 = 100%).</summary>
    public double Zoom { get; init; } = 1.0;

    /// <summary>Canvas width in pixels.</summary>
    public double Width { get; init; } = 800;

    /// <summary>Canvas height in pixels.</summary>
    public double Height { get; init; } = 600;
}

/// <summary>
/// Represents a node being dragged from the palette.
/// </summary>
public record DraggedNode
{
    /// <summary>The node definition being dragged.</summary>
    public NodeDefinition Definition { get; init; } = null!;

    /// <summary>Current drag position X.</summary>
    public double X { get; init; }

    /// <summary>Current drag position Y.</summary>
    public double Y { get; init; }
}

/// <summary>
/// Represents a connection being drawn.
/// </summary>
public record PendingConnection
{
    /// <summary>Source node ID.</summary>
    public string SourceNodeId { get; init; } = string.Empty;

    /// <summary>Source port name.</summary>
    public string SourcePort { get; init; } = string.Empty;

    /// <summary>Current mouse X position.</summary>
    public double CurrentX { get; init; }

    /// <summary>Current mouse Y position.</summary>
    public double CurrentY { get; init; }
}

/// <summary>
/// Represents a visual node on the canvas with computed properties.
/// </summary>
public record CanvasNode
{
    /// <summary>The underlying workflow node.</summary>
    public WorkflowNode Node { get; init; } = null!;

    /// <summary>The node definition for this node type.</summary>
    public NodeDefinition? Definition { get; init; }

    /// <summary>Whether this node is currently selected.</summary>
    public bool IsSelected { get; init; }

    /// <summary>Whether this node is being dragged.</summary>
    public bool IsDragging { get; init; }

    /// <summary>Node width for rendering.</summary>
    public double Width { get; init; } = 180;

    /// <summary>Node height for rendering.</summary>
    public double Height { get; init; } = 80;
}

/// <summary>
/// Represents a visual connection on the canvas.
/// </summary>
public record CanvasConnection
{
    /// <summary>The underlying connection.</summary>
    public Connection Connection { get; init; } = null!;

    /// <summary>Whether this connection is currently selected.</summary>
    public bool IsSelected { get; init; }

    /// <summary>Start X position (computed from source node).</summary>
    public double StartX { get; init; }

    /// <summary>Start Y position (computed from source node).</summary>
    public double StartY { get; init; }

    /// <summary>End X position (computed from target node).</summary>
    public double EndX { get; init; }

    /// <summary>End Y position (computed from target node).</summary>
    public double EndY { get; init; }
}

/// <summary>
/// Represents an undo/redo action.
/// </summary>
public record CanvasAction
{
    /// <summary>Type of action performed.</summary>
    public CanvasActionType Type { get; init; }

    /// <summary>Description of the action.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>State before the action (for undo).</summary>
    public Workflow? PreviousState { get; init; }

    /// <summary>State after the action (for redo).</summary>
    public Workflow? NewState { get; init; }
}

/// <summary>
/// Types of canvas actions for undo/redo.
/// </summary>
public enum CanvasActionType
{
    AddNode,
    RemoveNode,
    MoveNode,
    AddConnection,
    RemoveConnection,
    UpdateNodeConfig,
    BatchOperation
}

/// <summary>
/// Represents the execution state of a node for visualization.
/// </summary>
public record NodeExecutionState
{
    /// <summary>ID of the node.</summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>Current execution status.</summary>
    public ExecutionStatus Status { get; init; }

    /// <summary>When execution started.</summary>
    public DateTime StartedAt { get; init; }

    /// <summary>When execution completed.</summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>Input data received by the node.</summary>
    public JsonElement? InputData { get; init; }

    /// <summary>Output data produced by the node.</summary>
    public JsonElement? OutputData { get; init; }

    /// <summary>Error message if node failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Duration of execution in milliseconds.</summary>
    public double? DurationMs => CompletedAt.HasValue
        ? (CompletedAt.Value - StartedAt).TotalMilliseconds
        : null;
}
