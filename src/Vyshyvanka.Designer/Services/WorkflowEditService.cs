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
    /// <summary>Loads a workflow into the designer.</summary>
    public void LoadWorkflow(Workflow workflow)
    {
        canvasState.SaveUndoState("Load Workflow");
        store.SetWorkflow(workflow);
        canvasState.ClearSelectionState();
        store.MarkDirty();
        // Reset dirty since we just loaded
        store.MarkAsSaved();
        executionState.ClearExecutionState();
        validationService.ValidateWorkflow();
        store.NotifyStateChanged();
    }

    /// <summary>Creates a new empty workflow.</summary>
    public void NewWorkflow()
    {
        canvasState.SaveUndoState("New Workflow");
        store.SetWorkflow(WorkflowStore.CreateEmptyWorkflow());
        canvasState.ClearSelectionState();
        canvasState.ResetCanvasState();
        store.MarkDirty();
        // Reset dirty since this is a fresh workflow
        store.MarkAsSaved();
        executionState.ClearExecutionState();
        validationService.ValidateWorkflow();
        store.NotifyStateChanged();
    }

    /// <summary>Adds a node to the workflow.</summary>
    public void AddNode(WorkflowNode node)
    {
        canvasState.SaveUndoState("Add Node");
        store.SetWorkflow(store.Workflow with
        {
            Nodes = [.. store.Workflow.Nodes, node],
            UpdatedAt = DateTime.UtcNow
        });
        canvasState.SetSelectedNodeId(node.Id);
        store.MarkDirty();
        validationService.ValidateWorkflow();
        store.NotifyStateChanged();
    }

    /// <summary>Removes a node from the workflow.</summary>
    public void RemoveNode(string nodeId)
    {
        canvasState.SaveUndoState("Remove Node");
        var connections = store.Workflow.Connections
            .Where(c => c.SourceNodeId != nodeId && c.TargetNodeId != nodeId)
            .ToList();

        store.SetWorkflow(store.Workflow with
        {
            Nodes = store.Workflow.Nodes.Where(n => n.Id != nodeId).ToList(),
            Connections = connections,
            UpdatedAt = DateTime.UtcNow
        });

        canvasState.ClearSelectedNodeIfMatches(nodeId);
        store.MarkDirty();
        validationService.ValidateWorkflow();
        store.NotifyStateChanged();
    }

    /// <summary>Updates a node's position.</summary>
    public void MoveNode(string nodeId, double x, double y)
    {
        var node = store.Workflow.Nodes.FirstOrDefault(n => n.Id == nodeId);
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
        canvasState.SaveUndoState("Update Node Configuration");
        var node = store.Workflow.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null) return;

        var updatedNode = node with { Configuration = configuration };
        var nodes = store.Workflow.Nodes.Select(n => n.Id == nodeId ? updatedNode : n).ToList();

        store.SetWorkflow(store.Workflow with
        {
            Nodes = nodes,
            UpdatedAt = DateTime.UtcNow
        });
        store.MarkDirty();
        validationService.ValidateWorkflow();
        store.NotifyStateChanged();
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

        canvasState.SaveUndoState("Add Connection");
        store.SetWorkflow(store.Workflow with
        {
            Connections = [.. store.Workflow.Connections, connection],
            UpdatedAt = DateTime.UtcNow
        });
        store.MarkDirty();
        validationService.ValidateWorkflow();
        store.NotifyStateChanged();
    }

    /// <summary>Removes a connection.</summary>
    public void RemoveConnection(Connection connection)
    {
        canvasState.SaveUndoState("Remove Connection");
        store.SetWorkflow(store.Workflow with
        {
            Connections = store.Workflow.Connections
                .Where(c => !(c.SourceNodeId == connection.SourceNodeId &&
                              c.SourcePort == connection.SourcePort &&
                              c.TargetNodeId == connection.TargetNodeId &&
                              c.TargetPort == connection.TargetPort))
                .ToList(),
            UpdatedAt = DateTime.UtcNow
        });

        canvasState.ClearSelectedConnectionIfMatches(connection);
        store.MarkDirty();
        validationService.ValidateWorkflow();
        store.NotifyStateChanged();
    }

    /// <summary>Completes or cancels the pending connection.</summary>
    public void EndConnection(string? targetNodeId = null, string? targetPort = null)
    {
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
        canvasState.SaveUndoState("Update Workflow Metadata");
        store.SetWorkflow(store.Workflow with
        {
            Name = name,
            Description = description ?? store.Workflow.Description,
            UpdatedAt = DateTime.UtcNow
        });
        store.MarkDirty();
        validationService.ValidateWorkflow();
        store.NotifyStateChanged();
    }

    /// <summary>Toggles the workflow active state.</summary>
    public void ToggleWorkflowActive()
    {
        canvasState.SaveUndoState("Toggle Workflow Active");
        store.SetWorkflow(store.Workflow with
        {
            IsActive = !store.Workflow.IsActive,
            UpdatedAt = DateTime.UtcNow
        });
        store.MarkDirty();
        store.NotifyStateChanged();
    }

    /// <summary>Sets the workflow active state.</summary>
    public void SetWorkflowActive(bool isActive)
    {
        if (store.Workflow.IsActive == isActive) return;

        canvasState.SaveUndoState(isActive ? "Activate Workflow" : "Deactivate Workflow");
        store.SetWorkflow(store.Workflow with
        {
            IsActive = isActive,
            UpdatedAt = DateTime.UtcNow
        });
        store.MarkDirty();
        store.NotifyStateChanged();
    }

    /// <summary>Updates a node's name.</summary>
    public void UpdateNodeName(string nodeId, string name)
    {
        var node = store.Workflow.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null || string.IsNullOrWhiteSpace(name)) return;

        canvasState.SaveUndoState("Update Node Name");
        var updatedNode = node with { Name = name };
        var nodes = store.Workflow.Nodes.Select(n => n.Id == nodeId ? updatedNode : n).ToList();

        store.SetWorkflow(store.Workflow with
        {
            Nodes = nodes,
            UpdatedAt = DateTime.UtcNow
        });
        store.MarkDirty();
        validationService.ValidateWorkflow();
        store.NotifyStateChanged();
    }

    /// <summary>Updates a node's credential.</summary>
    public void UpdateNodeCredential(string nodeId, Guid? credentialId)
    {
        var node = store.Workflow.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null) return;

        canvasState.SaveUndoState("Update Node Credential");
        var updatedNode = node with { CredentialId = credentialId };
        var nodes = store.Workflow.Nodes.Select(n => n.Id == nodeId ? updatedNode : n).ToList();

        store.SetWorkflow(store.Workflow with
        {
            Nodes = nodes,
            UpdatedAt = DateTime.UtcNow
        });
        store.MarkDirty();
        store.NotifyStateChanged();
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
