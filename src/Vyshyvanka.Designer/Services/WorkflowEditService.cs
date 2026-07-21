using System.Text.Json;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// Manages workflow mutations: adding/removing/moving nodes,
/// managing connections, and updating workflow metadata.
/// </summary>
public class WorkflowEditService(
    WorkflowStore store,
    CanvasStateService canvasState,
    WorkflowValidationService validationService,
    ExecutionStateService executionState)
{
    /// <summary>
    /// Applies a workflow mutation with undo tracking, dirty marking, validation, and notification.
    /// All intermediate notifications are coalesced into a single re-render.
    /// </summary>
    private void CommitChange(string description, Func<Workflow, Workflow> transform, bool validate = true)
    {
        using var _ = store.SuspendNotifications();
        canvasState.SaveUndoState(description);
        store.SetWorkflow(transform(store.Workflow));
        store.MarkDirty();
        if (validate)
            validationService.ValidateWorkflow();
        store.NotifyStateChanged();
    }

    /// <summary>Loads a workflow into the designer.</summary>
    public void LoadWorkflow(Workflow workflow)
    {
        using var _ = store.SuspendNotifications();
        canvasState.SaveUndoState("Load Workflow");
        store.SetWorkflow(workflow);
        canvasState.ClearSelectionState();
        store.MarkAsSaved();
        executionState.ClearExecutionState();
        validationService.ValidateWorkflow();
        store.NotifyStateChanged();
    }

    /// <summary>Creates a new empty workflow.</summary>
    public void NewWorkflow()
    {
        using var _ = store.SuspendNotifications();
        canvasState.SaveUndoState("New Workflow");
        store.SetWorkflow(WorkflowStore.CreateEmptyWorkflow());
        canvasState.ClearSelectionState();
        canvasState.ResetCanvasState();
        store.MarkAsSaved();
        executionState.ClearExecutionState();
        validationService.ValidateWorkflow();
        store.NotifyStateChanged();
    }

    /// <summary>Adds a node to the workflow.</summary>
    public void AddNode(WorkflowNode node)
    {
        CommitChange("Add Node", w => w with
        {
            Nodes = [.. w.Nodes, node],
            UpdatedAt = DateTime.UtcNow
        });
        canvasState.SetSelectedNodeId(node.Id);
    }

    /// <summary>Removes a node from the workflow.</summary>
    public void RemoveNode(string nodeId)
    {
        CommitChange("Remove Node", w => w with
        {
            Nodes = w.Nodes.Where(n => n.Id != nodeId).ToList(),
            Connections = w.Connections
                .Where(c => c.SourceNodeId != nodeId && c.TargetNodeId != nodeId)
                .ToList(),
            UpdatedAt = DateTime.UtcNow
        });
        canvasState.ClearSelectedNodeIfMatches(nodeId);
    }

    /// <summary>Updates a node's position (no undo — called continuously during drag).</summary>
    public void MoveNode(string nodeId, double x, double y)
    {
        var node = store.GetNode(nodeId);
        if (node is null) return;

        var updatedNode = node with { Position = new Position(x, y) };
        var nodes = store.Workflow.Nodes.Select(n => n.Id == nodeId ? updatedNode : n).ToList();

        store.SetWorkflow(store.Workflow with
        {
            Nodes = nodes,
            UpdatedAt = DateTime.UtcNow
        });
        store.NotifyStateChanged();
    }

    /// <summary>Updates a node's configuration.</summary>
    public void UpdateNodeConfiguration(string nodeId, JsonElement configuration)
    {
        var node = store.GetNode(nodeId);
        if (node is null) return;

        CommitChange("Update Node Configuration", w => w with
        {
            Nodes = w.Nodes.Select(n => n.Id == nodeId ? n with { Configuration = configuration } : n).ToList(),
            UpdatedAt = DateTime.UtcNow
        });
    }

    /// <summary>Adds a connection between nodes.</summary>
    public void AddConnection(Connection connection)
    {
        // Check if connection already exists
        if (store.Workflow.Connections.Any(c =>
                c.SourceNodeId == connection.SourceNodeId &&
                c.SourcePort == connection.SourcePort &&
                c.TargetNodeId == connection.TargetNodeId &&
                c.TargetPort == connection.TargetPort))
            return;

        CommitChange("Add Connection", w => w with
        {
            Connections = [.. w.Connections, connection],
            UpdatedAt = DateTime.UtcNow
        });
    }

    /// <summary>Removes a connection.</summary>
    public void RemoveConnection(Connection connection)
    {
        CommitChange("Remove Connection", w => w with
        {
            Connections = w.Connections
                .Where(c => !(c.SourceNodeId == connection.SourceNodeId &&
                              c.SourcePort == connection.SourcePort &&
                              c.TargetNodeId == connection.TargetNodeId &&
                              c.TargetPort == connection.TargetPort))
                .ToList(),
            UpdatedAt = DateTime.UtcNow
        });
        canvasState.ClearSelectedConnectionIfMatches(connection);
    }

    /// <summary>Completes or cancels the pending connection.</summary>
    public void EndConnection(string? targetNodeId = null, string? targetPort = null)
    {
        using var _ = store.SuspendNotifications();
        var pending = canvasState.ConsumePendingConnection();
        if (pending is not null && targetNodeId is not null && targetPort is not null)
        {
            if (validationService.IsValidConnection(pending.SourceNodeId, pending.SourcePort, targetNodeId, targetPort))
            {
                var connection = new Connection
                {
                    SourceNodeId = pending.SourceNodeId,
                    SourcePort = pending.SourcePort,
                    TargetNodeId = targetNodeId,
                    TargetPort = targetPort
                };
                AddConnection(connection);
            }
        }

        store.NotifyStateChanged();
    }

    /// <summary>Updates workflow metadata.</summary>
    public void UpdateWorkflowMetadata(string name, string? description = null)
    {
        CommitChange("Update Workflow Metadata", w => w with
        {
            Name = name,
            Description = description ?? w.Description,
            UpdatedAt = DateTime.UtcNow
        });
    }

    /// <summary>Toggles the workflow active state.</summary>
    public void ToggleWorkflowActive()
    {
        CommitChange("Toggle Workflow Active", w => w with
        {
            IsActive = !w.IsActive,
            UpdatedAt = DateTime.UtcNow
        }, validate: false);
    }

    /// <summary>Sets the workflow active state.</summary>
    public void SetWorkflowActive(bool isActive)
    {
        if (store.Workflow.IsActive == isActive) return;

        CommitChange(isActive ? "Activate Workflow" : "Deactivate Workflow", w => w with
        {
            IsActive = isActive,
            UpdatedAt = DateTime.UtcNow
        }, validate: false);
    }

    /// <summary>Updates a node's name.</summary>
    public void UpdateNodeName(string nodeId, string name)
    {
        var node = store.GetNode(nodeId);
        if (node is null || string.IsNullOrWhiteSpace(name)) return;

        CommitChange("Update Node Name", w => w with
        {
            Nodes = w.Nodes.Select(n => n.Id == nodeId ? n with { Name = name } : n).ToList(),
            UpdatedAt = DateTime.UtcNow
        });
    }

    /// <summary>Updates a node's credential.</summary>
    public void UpdateNodeCredential(string nodeId, Guid? credentialId)
    {
        var node = store.GetNode(nodeId);
        if (node is null) return;

        CommitChange("Update Node Credential", w => w with
        {
            Nodes = w.Nodes.Select(n => n.Id == nodeId ? n with { CredentialId = credentialId } : n).ToList(),
            UpdatedAt = DateTime.UtcNow
        }, validate: false);
    }

    /// <summary>Drops a node from the palette onto the canvas.</summary>
    public void DropNodeFromPalette(double x, double y)
    {
        var draggedNodeType = canvasState.DraggedNodeType;
        if (draggedNodeType is null) return;

        var definition = store.GetNodeDefinition(draggedNodeType);
        if (definition is null)
        {
            canvasState.EndDragFromPalette();
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
        canvasState.EndDragFromPalette();
    }
}
