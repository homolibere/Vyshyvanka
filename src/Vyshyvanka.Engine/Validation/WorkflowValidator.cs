using System.ComponentModel.DataAnnotations;
using Vyshyvanka.Core.Models;
using ValidationResult = Vyshyvanka.Core.Models.ValidationResult;

namespace Vyshyvanka.Engine.Validation;

/// <summary>
/// Validates workflow definitions for schema compliance and structural integrity.
/// </summary>
public class WorkflowValidator
{
    /// <summary>
    /// Validates a workflow definition.
    /// </summary>
    /// <param name="workflow">The workflow to validate.</param>
    /// <returns>Validation result with any errors found.</returns>
    public ValidationResult Validate(Workflow workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        
        var errors = new List<ValidationError>();
        
        ValidateWorkflowFields(workflow, errors);
        ValidateNodes(workflow, errors);
        ValidateConnections(workflow, errors);
        
        return errors.Count == 0 
            ? ValidationResult.Success() 
            : ValidationResult.Failure([.. errors]);
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
        else if (workflow.Name.Length > 200)
        {
            errors.Add(new ValidationError
            {
                Path = "name",
                Message = "Workflow name cannot exceed 200 characters",
                ErrorCode = "WORKFLOW_NAME_TOO_LONG"
            });
        }

        if (workflow.Description?.Length > 2000)
        {
            errors.Add(new ValidationError
            {
                Path = "description",
                Message = "Description cannot exceed 2000 characters",
                ErrorCode = "WORKFLOW_DESCRIPTION_TOO_LONG"
            });
        }

        if (workflow.Version < 0)
        {
            errors.Add(new ValidationError
            {
                Path = "version",
                Message = "Version must be non-negative",
                ErrorCode = "WORKFLOW_VERSION_INVALID"
            });
        }
    }

    private static void ValidateNodes(Workflow workflow, List<ValidationError> errors)
    {
        var nodeIds = new HashSet<string>();
        
        for (int i = 0; i < workflow.Nodes.Count; i++)
        {
            var node = workflow.Nodes[i];
            var nodePath = $"nodes[{i}]";
            
            ValidateNodeFields(node, nodePath, errors);
            ValidateDuplicateNodeId(node, nodeIds, nodePath, errors);
        }
        
        ValidateTriggerNodes(workflow, errors);
    }

    private static void ValidateTriggerNodes(Workflow workflow, List<ValidationError> errors)
    {
        var triggerNodes = workflow.Nodes
            .Where(n => !string.IsNullOrWhiteSpace(n.Type) && 
                        n.Type.EndsWith("-trigger", StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        if (triggerNodes.Count == 0)
        {
            errors.Add(new ValidationError
            {
                Path = "nodes",
                Message = "Workflow must have exactly one trigger node",
                ErrorCode = "WORKFLOW_TRIGGER_REQUIRED"
            });
        }
        else if (triggerNodes.Count > 1)
        {
            errors.Add(new ValidationError
            {
                Path = "nodes",
                Message = $"Workflow must have exactly one trigger node, but found {triggerNodes.Count}",
                ErrorCode = "WORKFLOW_MULTIPLE_TRIGGERS"
            });
        }
    }

    private static void ValidateNodeFields(WorkflowNode node, string nodePath, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(node.Id))
        {
            errors.Add(new ValidationError
            {
                Path = $"{nodePath}.id",
                Message = "Node ID is required",
                ErrorCode = "NODE_ID_REQUIRED"
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
        else if (node.Name.Length > 200)
        {
            errors.Add(new ValidationError
            {
                Path = $"{nodePath}.name",
                Message = "Node name cannot exceed 200 characters",
                ErrorCode = "NODE_NAME_TOO_LONG"
            });
        }
    }

    private static void ValidateDuplicateNodeId(WorkflowNode node, HashSet<string> nodeIds, string nodePath, List<ValidationError> errors)
    {
        if (!string.IsNullOrWhiteSpace(node.Id))
        {
            if (!nodeIds.Add(node.Id))
            {
                errors.Add(new ValidationError
                {
                    Path = $"{nodePath}.id",
                    Message = $"Duplicate node ID: '{node.Id}'",
                    ErrorCode = "NODE_ID_DUPLICATE"
                });
            }
        }
    }

    private static void ValidateConnections(Workflow workflow, List<ValidationError> errors)
    {
        var nodeIds = workflow.Nodes
            .Where(n => !string.IsNullOrWhiteSpace(n.Id))
            .Select(n => n.Id)
            .ToHashSet();
        
        for (int i = 0; i < workflow.Connections.Count; i++)
        {
            var connection = workflow.Connections[i];
            var connectionPath = $"connections[{i}]";
            
            ValidateConnectionFields(connection, connectionPath, errors);
            ValidateConnectionReferences(connection, nodeIds, connectionPath, errors);
            ValidateSelfLoop(connection, connectionPath, errors);
        }
    }

    private static void ValidateConnectionFields(Connection connection, string connectionPath, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(connection.SourceNodeId))
        {
            errors.Add(new ValidationError
            {
                Path = $"{connectionPath}.sourceNodeId",
                Message = "Source node ID is required",
                ErrorCode = "CONNECTION_SOURCE_REQUIRED"
            });
        }

        if (string.IsNullOrWhiteSpace(connection.SourcePort))
        {
            errors.Add(new ValidationError
            {
                Path = $"{connectionPath}.sourcePort",
                Message = "Source port is required",
                ErrorCode = "CONNECTION_SOURCE_PORT_REQUIRED"
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

        if (string.IsNullOrWhiteSpace(connection.TargetPort))
        {
            errors.Add(new ValidationError
            {
                Path = $"{connectionPath}.targetPort",
                Message = "Target port is required",
                ErrorCode = "CONNECTION_TARGET_PORT_REQUIRED"
            });
        }
    }

    private static void ValidateConnectionReferences(Connection connection, HashSet<string> nodeIds, string connectionPath, List<ValidationError> errors)
    {
        if (!string.IsNullOrWhiteSpace(connection.SourceNodeId) && !nodeIds.Contains(connection.SourceNodeId))
        {
            errors.Add(new ValidationError
            {
                Path = $"{connectionPath}.sourceNodeId",
                Message = $"Source node '{connection.SourceNodeId}' does not exist in workflow",
                ErrorCode = "CONNECTION_SOURCE_NOT_FOUND"
            });
        }

        if (!string.IsNullOrWhiteSpace(connection.TargetNodeId) && !nodeIds.Contains(connection.TargetNodeId))
        {
            errors.Add(new ValidationError
            {
                Path = $"{connectionPath}.targetNodeId",
                Message = $"Target node '{connection.TargetNodeId}' does not exist in workflow",
                ErrorCode = "CONNECTION_TARGET_NOT_FOUND"
            });
        }
    }

    private static void ValidateSelfLoop(Connection connection, string connectionPath, List<ValidationError> errors)
    {
        if (!string.IsNullOrWhiteSpace(connection.SourceNodeId) && 
            !string.IsNullOrWhiteSpace(connection.TargetNodeId) &&
            connection.SourceNodeId == connection.TargetNodeId)
        {
            errors.Add(new ValidationError
            {
                Path = connectionPath,
                Message = $"Connection cannot connect a node to itself: '{connection.SourceNodeId}'",
                ErrorCode = "CONNECTION_SELF_LOOP"
            });
        }
    }
}
