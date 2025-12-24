using System.Text.Json;
using CsCheck;
using FlowForge.Core.Enums;
using FlowForge.Core.Models;
using FlowForge.Engine.Validation;

namespace FlowForge.Tests.Property;

/// <summary>
/// Property-based tests for workflow validation.
/// Feature: flowforge, Property 2: Workflow Schema Validation
/// </summary>
public class WorkflowValidationTests
{
    private readonly WorkflowValidator _validator = new();

    /// <summary>
    /// Feature: flowforge, Property 2: Workflow Schema Validation
    /// For any valid Workflow object (conforming to schema), validation SHALL return success.
    /// Validates: Requirements 1.4, 1.5
    /// </summary>
    [Fact]
    public void ValidWorkflow_PassesValidation()
    {
        GenValidWorkflow.Sample(workflow =>
        {
            // Act
            var result = _validator.Validate(workflow);

            // Assert
            Assert.True(result.IsValid, 
                $"Expected valid workflow to pass validation. Errors: {FormatErrors(result.Errors)}");
            Assert.Empty(result.Errors);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 2: Workflow Schema Validation
    /// For any workflow with missing name, validation SHALL return failure with descriptive error.
    /// Validates: Requirements 1.4, 1.5
    /// </summary>
    [Fact]
    public void WorkflowWithMissingName_FailsWithDescriptiveError()
    {
        GenWorkflowWithMissingName.Sample(workflow =>
        {
            // Act
            var result = _validator.Validate(workflow);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => 
                e.Path == "name" && 
                !string.IsNullOrWhiteSpace(e.Message) &&
                !string.IsNullOrWhiteSpace(e.ErrorCode));
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 2: Workflow Schema Validation
    /// For any workflow with name exceeding max length, validation SHALL return failure with descriptive error.
    /// Validates: Requirements 1.4, 1.5
    /// </summary>
    [Fact]
    public void WorkflowWithNameTooLong_FailsWithDescriptiveError()
    {
        GenWorkflowWithNameTooLong.Sample(workflow =>
        {
            // Act
            var result = _validator.Validate(workflow);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => 
                e.Path == "name" && 
                e.ErrorCode == "WORKFLOW_NAME_TOO_LONG" &&
                !string.IsNullOrWhiteSpace(e.Message));
        }, iter: 100);
    }


    /// <summary>
    /// Feature: flowforge, Property 2: Workflow Schema Validation
    /// For any workflow with duplicate node IDs, validation SHALL return failure with descriptive error.
    /// Validates: Requirements 1.4, 1.5
    /// </summary>
    [Fact]
    public void WorkflowWithDuplicateNodeIds_FailsWithDescriptiveError()
    {
        GenWorkflowWithDuplicateNodeIds.Sample(workflow =>
        {
            // Act
            var result = _validator.Validate(workflow);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => 
                e.Path.Contains("id") && 
                e.ErrorCode == "NODE_ID_DUPLICATE" &&
                !string.IsNullOrWhiteSpace(e.Message));
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 2: Workflow Schema Validation
    /// For any workflow with connection referencing non-existent node, validation SHALL return failure.
    /// Validates: Requirements 1.4, 1.5
    /// </summary>
    [Fact]
    public void WorkflowWithInvalidConnectionReference_FailsWithDescriptiveError()
    {
        GenWorkflowWithInvalidConnectionReference.Sample(workflow =>
        {
            // Act
            var result = _validator.Validate(workflow);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => 
                (e.ErrorCode == "CONNECTION_SOURCE_NOT_FOUND" || e.ErrorCode == "CONNECTION_TARGET_NOT_FOUND") &&
                !string.IsNullOrWhiteSpace(e.Message) &&
                !string.IsNullOrWhiteSpace(e.Path));
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 2: Workflow Schema Validation
    /// For any workflow with self-loop connection, validation SHALL return failure with descriptive error.
    /// Validates: Requirements 1.4, 1.5
    /// </summary>
    [Fact]
    public void WorkflowWithSelfLoopConnection_FailsWithDescriptiveError()
    {
        GenWorkflowWithSelfLoop.Sample(workflow =>
        {
            // Act
            var result = _validator.Validate(workflow);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => 
                e.ErrorCode == "CONNECTION_SELF_LOOP" &&
                !string.IsNullOrWhiteSpace(e.Message));
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 2: Workflow Schema Validation
    /// For any workflow with missing node fields, validation SHALL return failure with descriptive errors.
    /// Validates: Requirements 1.4, 1.5
    /// </summary>
    [Fact]
    public void WorkflowWithMissingNodeFields_FailsWithDescriptiveError()
    {
        GenWorkflowWithMissingNodeFields.Sample(workflow =>
        {
            // Act
            var result = _validator.Validate(workflow);

            // Assert
            Assert.False(result.IsValid);
            Assert.True(result.Errors.Count > 0);
            Assert.All(result.Errors, e =>
            {
                Assert.False(string.IsNullOrWhiteSpace(e.Path));
                Assert.False(string.IsNullOrWhiteSpace(e.Message));
                Assert.False(string.IsNullOrWhiteSpace(e.ErrorCode));
            });
        }, iter: 100);
    }

    private static string FormatErrors(List<ValidationError> errors) =>
        string.Join("; ", errors.Select(e => $"[{e.Path}] {e.ErrorCode}: {e.Message}"));


    #region Generators

    /// <summary>Generator for non-empty alphanumeric strings.</summary>
    private static Gen<string> GenNonEmptyString(int minLength, int maxLength) =>
        Gen.Char['a', 'z'].Array[minLength, maxLength].Select(chars => new string(chars));

    /// <summary>Generator for Position.</summary>
    private static readonly Gen<Position> GenPosition =
        from x in Gen.Double[-1000, 1000]
        from y in Gen.Double[-1000, 1000]
        select new Position(x, y);

    /// <summary>Generator for simple JSON configuration.</summary>
    private static readonly Gen<JsonElement> GenConfiguration =
        Gen.Const(JsonDocument.Parse("{}").RootElement);

    /// <summary>Generator for ErrorHandlingMode.</summary>
    private static readonly Gen<ErrorHandlingMode> GenErrorHandlingMode =
        Gen.OneOf(
            Gen.Const(ErrorHandlingMode.StopOnFirstError),
            Gen.Const(ErrorHandlingMode.ContinueOnError),
            Gen.Const(ErrorHandlingMode.RetryWithBackoff)
        );

    /// <summary>Generator for WorkflowSettings.</summary>
    private static readonly Gen<WorkflowSettings> GenWorkflowSettings =
        from maxRetries in Gen.Int[0, 10]
        from errorHandling in GenErrorHandlingMode
        select new WorkflowSettings
        {
            Timeout = TimeSpan.FromMinutes(5),
            MaxRetries = maxRetries,
            ErrorHandling = errorHandling
        };

    /// <summary>Generator for a valid WorkflowNode with unique ID.</summary>
    private static Gen<WorkflowNode> GenValidNode(string id) =>
        from type in GenNonEmptyString(1, 30)
        from name in GenNonEmptyString(1, 50)
        from position in GenPosition
        select new WorkflowNode
        {
            Id = id,
            Type = type,
            Name = name,
            Position = position,
            Configuration = JsonDocument.Parse("{}").RootElement,
            CredentialId = null
        };

    /// <summary>Generator for a list of valid nodes with unique IDs.</summary>
    private static readonly Gen<List<WorkflowNode>> GenValidNodes =
        Gen.Int[0, 5].SelectMany(count =>
        {
            if (count == 0) return Gen.Const(new List<WorkflowNode>());
            
            var nodeIds = Enumerable.Range(1, count).Select(i => $"node{i}").ToList();
            return nodeIds
                .Select(id => GenValidNode(id))
                .Aggregate(
                    Gen.Const(new List<WorkflowNode>()),
                    (acc, nodeGen) => acc.SelectMany(list => nodeGen.Select(node => 
                    {
                        list.Add(node);
                        return list;
                    }))
                );
        });


    /// <summary>Generator for valid connections between existing nodes.</summary>
    private static Gen<List<Connection>> GenValidConnections(List<WorkflowNode> nodes)
    {
        if (nodes.Count < 2) return Gen.Const(new List<Connection>());
        
        var nodeIds = nodes.Select(n => n.Id).ToList();
        return Gen.Int[0, Math.Min(nodes.Count - 1, 3)].SelectMany(count =>
        {
            if (count == 0) return Gen.Const(new List<Connection>());
            
            return Gen.Const(Enumerable.Range(0, count)
                .Where(i => i + 1 < nodeIds.Count)
                .Select(i => new Connection
                {
                    SourceNodeId = nodeIds[i],
                    SourcePort = "output",
                    TargetNodeId = nodeIds[i + 1],
                    TargetPort = "input"
                })
                .ToList());
        });
    }

    /// <summary>Generator for optional description.</summary>
    private static readonly Gen<string?> GenOptionalDescription =
        Gen.Bool.SelectMany(hasValue =>
            hasValue
                ? GenNonEmptyString(1, 100).Select(s => (string?)s)
                : Gen.Const((string?)null));

    /// <summary>Generator for valid Workflow.</summary>
    private static readonly Gen<Workflow> GenValidWorkflow =
        from id in Gen.Guid
        from name in GenNonEmptyString(1, 50)
        from description in GenOptionalDescription
        from version in Gen.Int[0, 100]
        from isActive in Gen.Bool
        from nodes in GenValidNodes
        from settings in GenWorkflowSettings
        from createdBy in Gen.Guid
        select new Workflow
        {
            Id = id,
            Name = name,
            Description = description,
            Version = version,
            IsActive = isActive,
            Nodes = nodes,
            Connections = GenerateValidConnectionsSync(nodes),
            Settings = settings,
            Tags = [],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

    private static List<Connection> GenerateValidConnectionsSync(List<WorkflowNode> nodes)
    {
        if (nodes.Count < 2) return [];
        
        var connections = new List<Connection>();
        for (int i = 0; i < nodes.Count - 1 && i < 3; i++)
        {
            connections.Add(new Connection
            {
                SourceNodeId = nodes[i].Id,
                SourcePort = "output",
                TargetNodeId = nodes[i + 1].Id,
                TargetPort = "input"
            });
        }
        return connections;
    }

    /// <summary>Generator for workflow with missing name.</summary>
    private static readonly Gen<Workflow> GenWorkflowWithMissingName =
        GenValidWorkflow.Select(w => w with { Name = "" });

    /// <summary>Generator for workflow with name too long (>200 chars).</summary>
    private static readonly Gen<Workflow> GenWorkflowWithNameTooLong =
        from workflow in GenValidWorkflow
        from longName in Gen.Char['a', 'z'].Array[201, 250].Select(chars => new string(chars))
        select workflow with { Name = longName };


    /// <summary>Generator for workflow with duplicate node IDs.</summary>
    private static readonly Gen<Workflow> GenWorkflowWithDuplicateNodeIds =
        from workflow in GenValidWorkflow
        from duplicateId in GenNonEmptyString(1, 20)
        let duplicateNodes = new List<WorkflowNode>
        {
            new WorkflowNode { Id = duplicateId, Type = "type1", Name = "Node 1", Position = new Position(0, 0), Configuration = JsonDocument.Parse("{}").RootElement },
            new WorkflowNode { Id = duplicateId, Type = "type2", Name = "Node 2", Position = new Position(100, 0), Configuration = JsonDocument.Parse("{}").RootElement }
        }
        select workflow with { Nodes = duplicateNodes, Connections = [] };

    /// <summary>Generator for workflow with connection referencing non-existent node.</summary>
    private static readonly Gen<Workflow> GenWorkflowWithInvalidConnectionReference =
        from workflow in GenValidWorkflow
        from nodeId in GenNonEmptyString(1, 20)
        let node = new WorkflowNode { Id = nodeId, Type = "type", Name = "Node", Position = new Position(0, 0), Configuration = JsonDocument.Parse("{}").RootElement }
        let invalidConnection = new Connection
        {
            SourceNodeId = nodeId,
            SourcePort = "output",
            TargetNodeId = "nonexistent_node_id",
            TargetPort = "input"
        }
        select workflow with { Nodes = [node], Connections = [invalidConnection] };

    /// <summary>Generator for workflow with self-loop connection.</summary>
    private static readonly Gen<Workflow> GenWorkflowWithSelfLoop =
        from workflow in GenValidWorkflow
        from nodeId in GenNonEmptyString(1, 20)
        let node = new WorkflowNode { Id = nodeId, Type = "type", Name = "Node", Position = new Position(0, 0), Configuration = JsonDocument.Parse("{}").RootElement }
        let selfLoopConnection = new Connection
        {
            SourceNodeId = nodeId,
            SourcePort = "output",
            TargetNodeId = nodeId,
            TargetPort = "input"
        }
        select workflow with { Nodes = [node], Connections = [selfLoopConnection] };

    /// <summary>Generator for workflow with missing node fields.</summary>
    private static readonly Gen<Workflow> GenWorkflowWithMissingNodeFields =
        from workflow in GenValidWorkflow
        from missingField in Gen.Int[0, 2]
        let invalidNode = missingField switch
        {
            0 => new WorkflowNode { Id = "", Type = "type", Name = "Node", Position = new Position(0, 0), Configuration = JsonDocument.Parse("{}").RootElement },
            1 => new WorkflowNode { Id = "id", Type = "", Name = "Node", Position = new Position(0, 0), Configuration = JsonDocument.Parse("{}").RootElement },
            _ => new WorkflowNode { Id = "id", Type = "type", Name = "", Position = new Position(0, 0), Configuration = JsonDocument.Parse("{}").RootElement }
        }
        select workflow with { Nodes = [invalidNode], Connections = [] };

    #endregion
}
