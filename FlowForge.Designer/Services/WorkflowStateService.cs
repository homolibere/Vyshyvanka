using System.Text.Json;
using System.Text.Json.Serialization;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using FlowForge.Designer.Models;

namespace FlowForge.Designer.Services;

/// <summary>
/// Manages the state of the workflow designer canvas.
/// </summary>
public class WorkflowStateService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private Workflow _workflow = CreateEmptyWorkflow();
    private CanvasState _canvasState = new();
    private string? _selectedNodeId;
    private Connection? _selectedConnection;
    private PendingConnection? _pendingConnection;
    private string? _draggedNodeType;
    private readonly Stack<CanvasAction> _undoStack = new();
    private readonly Stack<CanvasAction> _redoStack = new();
    private readonly List<NodeDefinition> _nodeDefinitions = [];
    private ValidationResult _validationResult = ValidationResult.Success();
    private ExecutionResponse? _currentExecution;
    private readonly Dictionary<string, NodeExecutionState> _nodeExecutionStates = new();
    private bool _isDirty;

    /// <summary>Event raised when the workflow state changes.</summary>
    public event Action? OnStateChanged;

    /// <summary>Event raised when validation state changes.</summary>
    public event Action<ValidationResult>? OnValidationChanged;

    /// <summary>Event raised when execution state changes.</summary>
    public event Action<ExecutionResponse?>? OnExecutionChanged;

    /// <summary>Gets the current workflow.</summary>
    public Workflow Workflow => _workflow;

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

    /// <summary>Gets available node definitions.</summary>
    public IReadOnlyList<NodeDefinition> NodeDefinitions => _nodeDefinitions;

    /// <summary>Gets whether undo is available.</summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>Gets whether redo is available.</summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Gets the current validation result.</summary>
    public ValidationResult ValidationResult => _validationResult;

    /// <summary>Gets whether the workflow has validation errors.</summary>
    public bool HasValidationErrors => !_validationResult.IsValid;

    /// <summary>Gets the current execution being visualized.</summary>
    public ExecutionResponse? CurrentExecution => _currentExecution;

    /// <summary>Gets whether an execution is currently being visualized.</summary>
    public bool IsExecutionActive => _currentExecution is not null &&
                                     (_currentExecution.Status == ExecutionStatus.Pending ||
                                      _currentExecution.Status == ExecutionStatus.Running);

    /// <summary>Gets whether the workflow has unsaved changes.</summary>
    public bool IsDirty => _isDirty;

    /// <summary>Gets the execution state for a specific node.</summary>
    public NodeExecutionState? GetNodeExecutionState(string nodeId) =>
        _nodeExecutionStates.TryGetValue(nodeId, out var state) ? state : null;

    /// <summary>Creates a new empty workflow.</summary>
    private static Workflow CreateEmptyWorkflow() => new()
    {
        Id = Guid.NewGuid(),
        Name = "New Workflow",
        Version = 1,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    /// <summary>Serializes the current workflow to JSON.</summary>
    public string SerializeToJson()
    {
        return JsonSerializer.Serialize(_workflow, SerializerOptions);
    }

    /// <summary>Deserializes a workflow from JSON.</summary>
    public static Workflow? DeserializeFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Workflow>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Marks the workflow as saved (not dirty).</summary>
    public void MarkAsSaved()
    {
        _isDirty = false;
        NotifyStateChanged();
    }

    /// <summary>Loads a workflow into the designer.</summary>
    public void LoadWorkflow(Workflow workflow)
    {
        SaveUndoState("Load Workflow");
        _workflow = workflow;
        _selectedNodeId = null;
        _selectedConnection = null;
        _pendingConnection = null;
        _isDirty = false;
        ClearExecutionState();
        ValidateWorkflow();
        NotifyStateChanged();
    }

    /// <summary>Creates a new empty workflow.</summary>
    public void NewWorkflow()
    {
        SaveUndoState("New Workflow");
        _workflow = CreateEmptyWorkflow();
        _selectedNodeId = null;
        _selectedConnection = null;
        _pendingConnection = null;
        _canvasState = new();
        _isDirty = false;
        ClearExecutionState();
        ValidateWorkflow();
        NotifyStateChanged();
    }

    /// <summary>Sets the available node definitions.</summary>
    public void SetNodeDefinitions(IEnumerable<NodeDefinition> definitions)
    {
        _nodeDefinitions.Clear();
        _nodeDefinitions.AddRange(definitions);
        NotifyStateChanged();
    }

    /// <summary>Adds a node to the workflow.</summary>
    public void AddNode(WorkflowNode node)
    {
        SaveUndoState("Add Node");
        _workflow = _workflow with
        {
            Nodes = [.. _workflow.Nodes, node],
            UpdatedAt = DateTime.UtcNow
        };
        _selectedNodeId = node.Id;
        _isDirty = true;
        ValidateWorkflow();
        NotifyStateChanged();
    }

    /// <summary>Removes a node from the workflow.</summary>
    public void RemoveNode(string nodeId)
    {
        SaveUndoState("Remove Node");
        var connections = _workflow.Connections
            .Where(c => c.SourceNodeId != nodeId && c.TargetNodeId != nodeId)
            .ToList();

        _workflow = _workflow with
        {
            Nodes = _workflow.Nodes.Where(n => n.Id != nodeId).ToList(),
            Connections = connections,
            UpdatedAt = DateTime.UtcNow
        };

        if (_selectedNodeId == nodeId)
            _selectedNodeId = null;

        _isDirty = true;
        ValidateWorkflow();
        NotifyStateChanged();
    }

    /// <summary>Updates a node's position.</summary>
    public void MoveNode(string nodeId, double x, double y)
    {
        var node = _workflow.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null) return;

        var updatedNode = node with { Position = new Position(x, y) };
        var nodes = _workflow.Nodes.Select(n => n.Id == nodeId ? updatedNode : n).ToList();

        _workflow = _workflow with
        {
            Nodes = nodes,
            UpdatedAt = DateTime.UtcNow
        };
        NotifyStateChanged();
    }


    /// <summary>Updates a node's configuration.</summary>
    public void UpdateNodeConfiguration(string nodeId, JsonElement configuration)
    {
        SaveUndoState("Update Node Configuration");
        var node = _workflow.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null) return;

        var updatedNode = node with { Configuration = configuration };
        var nodes = _workflow.Nodes.Select(n => n.Id == nodeId ? updatedNode : n).ToList();

        _workflow = _workflow with
        {
            Nodes = nodes,
            UpdatedAt = DateTime.UtcNow
        };
        _isDirty = true;
        ValidateWorkflow();
        NotifyStateChanged();
    }

    /// <summary>Adds a connection between nodes.</summary>
    public void AddConnection(Connection connection)
    {
        // Check if connection already exists
        if (_workflow.Connections.Any(c =>
                c.SourceNodeId == connection.SourceNodeId &&
                c.SourcePort == connection.SourcePort &&
                c.TargetNodeId == connection.TargetNodeId &&
                c.TargetPort == connection.TargetPort))
            return;

        SaveUndoState("Add Connection");
        _workflow = _workflow with
        {
            Connections = [.. _workflow.Connections, connection],
            UpdatedAt = DateTime.UtcNow
        };
        _isDirty = true;
        ValidateWorkflow();
        NotifyStateChanged();
    }

    /// <summary>Removes a connection.</summary>
    public void RemoveConnection(Connection connection)
    {
        SaveUndoState("Remove Connection");
        _workflow = _workflow with
        {
            Connections = _workflow.Connections
                .Where(c => !(c.SourceNodeId == connection.SourceNodeId &&
                              c.SourcePort == connection.SourcePort &&
                              c.TargetNodeId == connection.TargetNodeId &&
                              c.TargetPort == connection.TargetPort))
                .ToList(),
            UpdatedAt = DateTime.UtcNow
        };

        if (_selectedConnection == connection)
            _selectedConnection = null;

        _isDirty = true;
        ValidateWorkflow();
        NotifyStateChanged();
    }

    /// <summary>Selects a node.</summary>
    public void SelectNode(string? nodeId)
    {
        _selectedNodeId = nodeId;
        _selectedConnection = null;
        NotifyStateChanged();
    }

    /// <summary>Selects a connection.</summary>
    public void SelectConnection(Connection? connection)
    {
        _selectedConnection = connection;
        _selectedNodeId = null;
        NotifyStateChanged();
    }

    /// <summary>Clears selection.</summary>
    public void ClearSelection()
    {
        _selectedNodeId = null;
        _selectedConnection = null;
        NotifyStateChanged();
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
        NotifyStateChanged();
    }

    /// <summary>Updates the pending connection position.</summary>
    public void UpdatePendingConnection(double x, double y)
    {
        if (_pendingConnection is null) return;
        _pendingConnection = _pendingConnection with { CurrentX = x, CurrentY = y };
        NotifyStateChanged();
    }

    /// <summary>Completes or cancels the pending connection.</summary>
    public void EndConnection(string? targetNodeId = null, string? targetPort = null)
    {
        if (_pendingConnection is not null && targetNodeId is not null && targetPort is not null)
        {
            // Validate the connection
            if (IsValidConnection(_pendingConnection.SourceNodeId, _pendingConnection.SourcePort, targetNodeId,
                    targetPort))
            {
                var connection = new Connection
                {
                    SourceNodeId = _pendingConnection.SourceNodeId,
                    SourcePort = _pendingConnection.SourcePort,
                    TargetNodeId = targetNodeId,
                    TargetPort = targetPort
                };
                AddConnection(connection);
            }
        }

        _pendingConnection = null;
        NotifyStateChanged();
    }

    /// <summary>Validates if a connection between two ports is valid.</summary>
    public bool IsValidConnection(string sourceNodeId, string sourcePort, string targetNodeId, string targetPort)
    {
        // Cannot connect a node to itself
        if (sourceNodeId == targetNodeId)
            return false;

        // Check if connection already exists
        if (_workflow.Connections.Any(c =>
                c.SourceNodeId == sourceNodeId &&
                c.SourcePort == sourcePort &&
                c.TargetNodeId == targetNodeId &&
                c.TargetPort == targetPort))
            return false;

        // Get port definitions
        var sourceNode = GetNode(sourceNodeId);
        var targetNode = GetNode(targetNodeId);
        if (sourceNode is null || targetNode is null)
            return false;

        var sourceDefinition = GetNodeDefinition(sourceNode.Type);
        var targetDefinition = GetNodeDefinition(targetNode.Type);
        if (sourceDefinition is null || targetDefinition is null)
            return false;

        var sourcePortDef = sourceDefinition.Outputs.FirstOrDefault(p => p.Name == sourcePort);
        var targetPortDef = targetDefinition.Inputs.FirstOrDefault(p => p.Name == targetPort);
        if (sourcePortDef is null || targetPortDef is null)
            return false;

        // Check port type compatibility
        return ArePortTypesCompatible(sourcePortDef.Type, targetPortDef.Type);
    }

    /// <summary>Checks if two port types are compatible for connection.</summary>
    private static bool ArePortTypesCompatible(PortType sourceType, PortType targetType)
    {
        // Any type is compatible with everything
        if (sourceType == PortType.Any || targetType == PortType.Any)
            return true;

        // Same types are always compatible
        if (sourceType == targetType)
            return true;

        // Object can connect to most types (loose typing)
        if (sourceType == PortType.Object)
            return true;

        // Array can connect to Object
        if (sourceType == PortType.Array && targetType == PortType.Object)
            return true;

        return false;
    }

    /// <summary>Updates the canvas pan position.</summary>
    public void Pan(double deltaX, double deltaY)
    {
        _canvasState = _canvasState with
        {
            PanX = _canvasState.PanX + deltaX,
            PanY = _canvasState.PanY + deltaY
        };
        NotifyStateChanged();
    }

    /// <summary>Updates the canvas zoom level.</summary>
    public void Zoom(double zoom, double? centerX = null, double? centerY = null)
    {
        var newZoom = Math.Clamp(zoom, 0.25, 2.0);
        _canvasState = _canvasState with { Zoom = newZoom };
        NotifyStateChanged();
    }

    /// <summary>Resets the canvas view.</summary>
    public void ResetView()
    {
        _canvasState = new CanvasState { Width = _canvasState.Width, Height = _canvasState.Height };
        NotifyStateChanged();
    }

    /// <summary>Updates the canvas size.</summary>
    public void SetCanvasSize(double width, double height)
    {
        _canvasState = _canvasState with { Width = width, Height = height };
        NotifyStateChanged();
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
                PreviousState = _workflow,
                NewState = action.PreviousState
            });
            _workflow = action.PreviousState;
            NotifyStateChanged();
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
                PreviousState = _workflow,
                NewState = action.PreviousState
            });
            _workflow = action.PreviousState;
            NotifyStateChanged();
        }
    }

    /// <summary>Saves the current state for undo.</summary>
    private void SaveUndoState(string description)
    {
        _undoStack.Push(new CanvasAction
        {
            Type = CanvasActionType.BatchOperation,
            Description = description,
            PreviousState = _workflow
        });
        _redoStack.Clear();
    }

    /// <summary>Gets a node definition by type.</summary>
    public NodeDefinition? GetNodeDefinition(string nodeType)
    {
        return _nodeDefinitions.FirstOrDefault(d => d.Type == nodeType);
    }

    /// <summary>Gets a node by ID.</summary>
    public WorkflowNode? GetNode(string nodeId)
    {
        return _workflow.Nodes.FirstOrDefault(n => n.Id == nodeId);
    }

    /// <summary>Gets the selected node.</summary>
    public WorkflowNode? GetSelectedNode()
    {
        return _selectedNodeId is not null ? GetNode(_selectedNodeId) : null;
    }

    /// <summary>Updates workflow metadata.</summary>
    public void UpdateWorkflowMetadata(string name, string? description = null)
    {
        SaveUndoState("Update Workflow Metadata");
        _workflow = _workflow with
        {
            Name = name,
            Description = description ?? _workflow.Description,
            UpdatedAt = DateTime.UtcNow
        };
        _isDirty = true;
        ValidateWorkflow();
        NotifyStateChanged();
    }

    /// <summary>Toggles the workflow active state.</summary>
    public void ToggleWorkflowActive()
    {
        SaveUndoState("Toggle Workflow Active");
        _workflow = _workflow with
        {
            IsActive = !_workflow.IsActive,
            UpdatedAt = DateTime.UtcNow
        };
        _isDirty = true;
        NotifyStateChanged();
    }

    /// <summary>Sets the workflow active state.</summary>
    public void SetWorkflowActive(bool isActive)
    {
        if (_workflow.IsActive == isActive) return;
        
        SaveUndoState(isActive ? "Activate Workflow" : "Deactivate Workflow");
        _workflow = _workflow with
        {
            IsActive = isActive,
            UpdatedAt = DateTime.UtcNow
        };
        _isDirty = true;
        NotifyStateChanged();
    }

    /// <summary>Updates a node's name.</summary>
    public void UpdateNodeName(string nodeId, string name)
    {
        var node = _workflow.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null || string.IsNullOrWhiteSpace(name)) return;

        SaveUndoState("Update Node Name");
        var updatedNode = node with { Name = name };
        var nodes = _workflow.Nodes.Select(n => n.Id == nodeId ? updatedNode : n).ToList();

        _workflow = _workflow with
        {
            Nodes = nodes,
            UpdatedAt = DateTime.UtcNow
        };
        _isDirty = true;
        ValidateWorkflow();
        NotifyStateChanged();
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

    /// <summary>Drops a node from the palette onto the canvas.</summary>
    public void DropNodeFromPalette(double x, double y)
    {
        if (_draggedNodeType is null) return;

        var definition = GetNodeDefinition(_draggedNodeType);
        if (definition is null)
        {
            _draggedNodeType = null;
            return;
        }

        var node = new WorkflowNode
        {
            Id = Guid.NewGuid().ToString(),
            Type = definition.Type,
            Name = definition.Name,
            Position = new Position(x, y)
        };

        AddNode(node);
        _draggedNodeType = null;
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();

    #region Validation

    /// <summary>Validates the current workflow and updates validation state.</summary>
    public void ValidateWorkflow()
    {
        var errors = new List<ValidationError>();

        // Validate workflow fields
        if (string.IsNullOrWhiteSpace(_workflow.Name))
        {
            errors.Add(new ValidationError
            {
                Path = "name",
                Message = "Workflow name is required",
                ErrorCode = "WORKFLOW_NAME_REQUIRED"
            });
        }

        // Validate nodes
        var nodeIds = new HashSet<string>();
        for (int i = 0; i < _workflow.Nodes.Count; i++)
        {
            var node = _workflow.Nodes[i];
            var nodePath = $"nodes[{i}]";

            if (string.IsNullOrWhiteSpace(node.Id))
            {
                errors.Add(new ValidationError
                {
                    Path = $"{nodePath}.id",
                    Message = "Node ID is required",
                    ErrorCode = "NODE_ID_REQUIRED"
                });
            }
            else if (!nodeIds.Add(node.Id))
            {
                errors.Add(new ValidationError
                {
                    Path = $"{nodePath}.id",
                    Message = $"Duplicate node ID: '{node.Id}'",
                    ErrorCode = "NODE_ID_DUPLICATE"
                });
            }

            if (string.IsNullOrWhiteSpace(node.Type))
            {
                errors.Add(new ValidationError
                {
                    Path = $"{nodePath}.type",
                    Message = "Node type is required",
                    ErrorCode = "NODE_TYPE_REQUIRED"
                });
            }

            if (string.IsNullOrWhiteSpace(node.Name))
            {
                errors.Add(new ValidationError
                {
                    Path = $"{nodePath}.name",
                    Message = "Node name is required",
                    ErrorCode = "NODE_NAME_REQUIRED"
                });
            }
        }

        // Validate connections
        for (int i = 0; i < _workflow.Connections.Count; i++)
        {
            var connection = _workflow.Connections[i];
            var connectionPath = $"connections[{i}]";

            if (string.IsNullOrWhiteSpace(connection.SourceNodeId))
            {
                errors.Add(new ValidationError
                {
                    Path = $"{connectionPath}.sourceNodeId",
                    Message = "Source node ID is required",
                    ErrorCode = "CONNECTION_SOURCE_REQUIRED"
                });
            }
            else if (!nodeIds.Contains(connection.SourceNodeId))
            {
                errors.Add(new ValidationError
                {
                    Path = $"{connectionPath}.sourceNodeId",
                    Message = $"Source node '{connection.SourceNodeId}' does not exist",
                    ErrorCode = "CONNECTION_SOURCE_NOT_FOUND"
                });
            }

            if (string.IsNullOrWhiteSpace(connection.TargetNodeId))
            {
                errors.Add(new ValidationError
                {
                    Path = $"{connectionPath}.targetNodeId",
                    Message = "Target node ID is required",
                    ErrorCode = "CONNECTION_TARGET_REQUIRED"
                });
            }
            else if (!nodeIds.Contains(connection.TargetNodeId))
            {
                errors.Add(new ValidationError
                {
                    Path = $"{connectionPath}.targetNodeId",
                    Message = $"Target node '{connection.TargetNodeId}' does not exist",
                    ErrorCode = "CONNECTION_TARGET_NOT_FOUND"
                });
            }

            if (!string.IsNullOrWhiteSpace(connection.SourceNodeId) &&
                !string.IsNullOrWhiteSpace(connection.TargetNodeId) &&
                connection.SourceNodeId == connection.TargetNodeId)
            {
                errors.Add(new ValidationError
                {
                    Path = connectionPath,
                    Message = "Connection cannot connect a node to itself",
                    ErrorCode = "CONNECTION_SELF_LOOP"
                });
            }
        }

        // Check for trigger nodes
        var hasTrigger = _workflow.Nodes.Any(n =>
        {
            var def = GetNodeDefinition(n.Type);
            return def?.Category == NodeCategory.Trigger;
        });

        if (_workflow.Nodes.Count > 0 && !hasTrigger)
        {
            errors.Add(new ValidationError
            {
                Path = "nodes",
                Message = "Workflow must have at least one trigger node",
                ErrorCode = "WORKFLOW_NO_TRIGGER"
            });
        }

        _validationResult = errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure([.. errors]);

        OnValidationChanged?.Invoke(_validationResult);
    }

    /// <summary>Gets validation errors for a specific node.</summary>
    public IEnumerable<ValidationError> GetNodeValidationErrors(string nodeId)
    {
        return _validationResult.Errors.Where(e =>
            e.Path.Contains($"nodes[") && e.Path.Contains(nodeId) ||
            e.Path.Contains($"sourceNodeId") && e.Message.Contains(nodeId) ||
            e.Path.Contains($"targetNodeId") && e.Message.Contains(nodeId));
    }

    #endregion

    #region Execution Visualization

    /// <summary>Sets the current execution for visualization.</summary>
    public void SetCurrentExecution(ExecutionResponse? execution)
    {
        _currentExecution = execution;
        UpdateNodeExecutionStates();
        OnExecutionChanged?.Invoke(execution);
        NotifyStateChanged();
    }

    /// <summary>Updates the current execution state (for polling).</summary>
    public void UpdateExecution(ExecutionResponse execution)
    {
        if (_currentExecution?.Id != execution.Id)
            return;

        _currentExecution = execution;
        UpdateNodeExecutionStates();
        OnExecutionChanged?.Invoke(execution);
        NotifyStateChanged();
    }

    /// <summary>Clears the current execution visualization.</summary>
    public void ClearExecutionState()
    {
        _currentExecution = null;
        _nodeExecutionStates.Clear();
        OnExecutionChanged?.Invoke(null);
        NotifyStateChanged();
    }

    /// <summary>Updates node execution states from the current execution.</summary>
    private void UpdateNodeExecutionStates()
    {
        _nodeExecutionStates.Clear();

        if (_currentExecution is null)
            return;

        foreach (var nodeExecution in _currentExecution.NodeExecutions)
        {
            _nodeExecutionStates[nodeExecution.NodeId] = new NodeExecutionState
            {
                NodeId = nodeExecution.NodeId,
                Status = nodeExecution.Status,
                StartedAt = nodeExecution.StartedAt,
                CompletedAt = nodeExecution.CompletedAt,
                InputData = nodeExecution.InputData,
                OutputData = nodeExecution.OutputData,
                ErrorMessage = nodeExecution.ErrorMessage
            };
        }
    }

    /// <summary>Gets all node execution states.</summary>
    public IReadOnlyDictionary<string, NodeExecutionState> GetAllNodeExecutionStates() => _nodeExecutionStates;

    #endregion
}
