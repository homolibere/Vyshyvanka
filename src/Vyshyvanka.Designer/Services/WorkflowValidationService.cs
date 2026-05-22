using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// Validates the current workflow and manages validation state.
/// </summary>
public class WorkflowValidationService(WorkflowStore store)
{
    private ValidationResult _validationResult = ValidationResult.Success();

    /// <summary>Event raised when validation state changes.</summary>
    public event Action<ValidationResult>? OnValidationChanged;

    /// <summary>Gets the current validation result.</summary>
    public ValidationResult ValidationResult => _validationResult;

    /// <summary>Gets whether the workflow has validation errors.</summary>
    public bool HasValidationErrors => !_validationResult.IsValid;

    /// <summary>Validates the current workflow and updates validation state.</summary>
    public void ValidateWorkflow()
    {
        var workflow = store.Workflow;
        var errors = new List<ValidationError>();

        ValidateWorkflowFields(workflow, errors);
        var nodeIds = ValidateNodes(workflow, errors);
        ValidateConnections(workflow, nodeIds, errors);
        ValidateTriggerPresence(workflow, errors);

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

    /// <summary>Validates if a connection between two ports is valid.</summary>
    public bool IsValidConnection(string sourceNodeId, string sourcePort, string targetNodeId, string targetPort)
    {
        var workflow = store.Workflow;

        // Cannot connect a node to itself
        if (sourceNodeId == targetNodeId)
            return false;

        // Check if connection already exists
        if (workflow.Connections.Any(c =>
                c.SourceNodeId == sourceNodeId &&
                c.SourcePort == sourcePort &&
                c.TargetNodeId == targetNodeId &&
                c.TargetPort == targetPort))
            return false;

        // Get port definitions
        var sourceNode = store.GetNode(sourceNodeId);
        var targetNode = store.GetNode(targetNodeId);
        if (sourceNode is null || targetNode is null)
            return false;

        var sourceDefinition = store.GetNodeDefinition(sourceNode.Type);
        var targetDefinition = store.GetNodeDefinition(targetNode.Type);
        if (sourceDefinition is null || targetDefinition is null)
            return false;

        var sourcePortDef = sourceDefinition.Outputs.FirstOrDefault(p => p.Name == sourcePort);
        var targetPortDef = targetDefinition.Inputs.FirstOrDefault(p => p.Name == targetPort);
        if (sourcePortDef is null || targetPortDef is null)
            return false;

        // Check port type compatibility
        return ArePortTypesCompatible(sourcePortDef.Type, targetPortDef.Type);
    }

    private static void ValidateWorkflowFields(Workflow workflow, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(workflow.Name))
        {
            errors.Add(new ValidationError
            {
                Path = "name",
                Message = "Workflow name is required",
                ErrorCode = "WORKFLOW_NAME_REQUIRED"
            });
        }
    }

    private static HashSet<string> ValidateNodes(Workflow workflow, List<ValidationError> errors)
    {
        var nodeIds = new HashSet<string>();
        for (int i = 0; i < workflow.Nodes.Count; i++)
        {
            var node = workflow.Nodes[i];
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

        return nodeIds;
    }

    private static void ValidateConnections(Workflow workflow, HashSet<string> nodeIds, List<ValidationError> errors)
    {
        for (int i = 0; i < workflow.Connections.Count; i++)
        {
            var connection = workflow.Connections[i];
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
    }

    private void ValidateTriggerPresence(Workflow workflow, List<ValidationError> errors)
    {
        var hasTrigger = workflow.Nodes.Any(n =>
        {
            var def = store.GetNodeDefinition(n.Type);
            return def?.Category == NodeCategory.Trigger;
        });

        if (workflow.Nodes.Count > 0 && !hasTrigger)
        {
            errors.Add(new ValidationError
            {
                Path = "nodes",
                Message = "Workflow must have at least one trigger node",
                ErrorCode = "WORKFLOW_NO_TRIGGER"
            });
        }
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
}
