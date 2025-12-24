using System.Text.Json;
using CsCheck;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using FlowForge.Designer.Services;

namespace FlowForge.Tests.Property;

/// <summary>
/// Property-based tests for connection port compatibility.
/// Feature: flowforge, Property 10: Connection Port Compatibility
/// </summary>
public class ConnectionPortCompatibilityTests
{
    /// <summary>
    /// Feature: flowforge, Property 10: Connection Port Compatibility
    /// For any connection where the source port type is Any, the connection SHALL be allowed
    /// regardless of the target port type.
    /// Validates: Requirements 4.3
    /// </summary>
    [Fact]
    public void AnySourceType_IsCompatibleWithAllTargetTypes()
    {
        GenPortType.Sample(targetType =>
        {
            // Arrange
            var stateService = CreateStateServiceWithNodes(PortType.Any, targetType);

            // Act
            var isValid = stateService.IsValidConnection("source-node", "output", "target-node", "input");

            // Assert
            Assert.True(isValid,
                $"Any source type should be compatible with {targetType} target type");
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 10: Connection Port Compatibility
    /// For any connection where the target port type is Any, the connection SHALL be allowed
    /// regardless of the source port type.
    /// Validates: Requirements 4.3
    /// </summary>
    [Fact]
    public void AnyTargetType_IsCompatibleWithAllSourceTypes()
    {
        GenPortType.Sample(sourceType =>
        {
            // Arrange
            var stateService = CreateStateServiceWithNodes(sourceType, PortType.Any);

            // Act
            var isValid = stateService.IsValidConnection("source-node", "output", "target-node", "input");

            // Assert
            Assert.True(isValid,
                $"{sourceType} source type should be compatible with Any target type");
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 10: Connection Port Compatibility
    /// For any connection where source and target port types are the same,
    /// the connection SHALL be allowed.
    /// Validates: Requirements 4.3
    /// </summary>
    [Fact]
    public void SamePortTypes_AreAlwaysCompatible()
    {
        GenPortType.Sample(portType =>
        {
            // Arrange
            var stateService = CreateStateServiceWithNodes(portType, portType);

            // Act
            var isValid = stateService.IsValidConnection("source-node", "output", "target-node", "input");

            // Assert
            Assert.True(isValid,
                $"Same port types ({portType}) should always be compatible");
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 10: Connection Port Compatibility
    /// For any connection where source port type is Object, the connection SHALL be allowed
    /// to any target type (loose typing).
    /// Validates: Requirements 4.3
    /// </summary>
    [Fact]
    public void ObjectSourceType_IsCompatibleWithAllTargetTypes()
    {
        GenPortType.Sample(targetType =>
        {
            // Arrange
            var stateService = CreateStateServiceWithNodes(PortType.Object, targetType);

            // Act
            var isValid = stateService.IsValidConnection("source-node", "output", "target-node", "input");

            // Assert
            Assert.True(isValid,
                $"Object source type should be compatible with {targetType} target type (loose typing)");
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 10: Connection Port Compatibility
    /// For any connection where source port type is Array and target is Object,
    /// the connection SHALL be allowed.
    /// Validates: Requirements 4.3
    /// </summary>
    [Fact]
    public void ArraySourceType_IsCompatibleWithObjectTarget()
    {
        // Arrange
        var stateService = CreateStateServiceWithNodes(PortType.Array, PortType.Object);

        // Act
        var isValid = stateService.IsValidConnection("source-node", "output", "target-node", "input");

        // Assert
        Assert.True(isValid,
            "Array source type should be compatible with Object target type");
    }

    /// <summary>
    /// Feature: flowforge, Property 10: Connection Port Compatibility
    /// For any connection between incompatible specific types (not Any, not Object source),
    /// the connection SHALL be rejected.
    /// Validates: Requirements 4.3
    /// </summary>
    [Fact]
    public void IncompatibleSpecificTypes_AreRejected()
    {
        GenIncompatiblePortTypePair.Sample(pair =>
        {
            // Arrange
            var stateService = CreateStateServiceWithNodes(pair.Source, pair.Target);

            // Act
            var isValid = stateService.IsValidConnection("source-node", "output", "target-node", "input");

            // Assert
            Assert.False(isValid,
                $"Incompatible types {pair.Source} -> {pair.Target} should be rejected");
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 10: Connection Port Compatibility
    /// For any connection attempt from a node to itself, the connection SHALL be rejected
    /// regardless of port types.
    /// Validates: Requirements 4.3
    /// </summary>
    [Fact]
    public void SelfConnection_IsAlwaysRejected()
    {
        GenPortTypePair.Sample(pair =>
        {
            // Arrange
            var stateService = CreateStateServiceWithSingleNode(pair.Source, pair.Target);

            // Act
            var isValid = stateService.IsValidConnection("single-node", "output", "single-node", "input");

            // Assert
            Assert.False(isValid,
                $"Self-connection should be rejected regardless of port types ({pair.Source} -> {pair.Target})");
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 10: Connection Port Compatibility
    /// For any duplicate connection attempt, the connection SHALL be rejected.
    /// Validates: Requirements 4.3
    /// </summary>
    [Fact]
    public void DuplicateConnection_IsRejected()
    {
        GenPortTypePair.Sample(pair =>
        {
            // Arrange
            var stateService = CreateStateServiceWithNodes(pair.Source, pair.Target);

            // First connection should succeed (if types are compatible)
            var firstIsValid = stateService.IsValidConnection("source-node", "output", "target-node", "input");
            if (firstIsValid)
            {
                stateService.AddConnection(new Connection
                {
                    SourceNodeId = "source-node",
                    SourcePort = "output",
                    TargetNodeId = "target-node",
                    TargetPort = "input"
                });

                // Act - Try to add duplicate
                var duplicateIsValid = stateService.IsValidConnection("source-node", "output", "target-node", "input");

                // Assert
                Assert.False(duplicateIsValid,
                    "Duplicate connection should be rejected");
            }
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 10: Connection Port Compatibility
    /// For any connection with non-existent source or target node, the connection SHALL be rejected.
    /// Validates: Requirements 4.3
    /// </summary>
    [Fact]
    public void ConnectionToNonExistentNode_IsRejected()
    {
        GenPortType.Sample(portType =>
        {
            // Arrange
            var stateService = CreateStateServiceWithNodes(portType, portType);

            // Act & Assert - Non-existent source
            var invalidSource = stateService.IsValidConnection("non-existent", "output", "target-node", "input");
            Assert.False(invalidSource, "Connection from non-existent source node should be rejected");

            // Act & Assert - Non-existent target
            var invalidTarget = stateService.IsValidConnection("source-node", "output", "non-existent", "input");
            Assert.False(invalidTarget, "Connection to non-existent target node should be rejected");
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 10: Connection Port Compatibility
    /// For any connection with non-existent port name, the connection SHALL be rejected.
    /// Validates: Requirements 4.3
    /// </summary>
    [Fact]
    public void ConnectionToNonExistentPort_IsRejected()
    {
        GenPortType.Sample(portType =>
        {
            // Arrange
            var stateService = CreateStateServiceWithNodes(portType, portType);

            // Act & Assert - Non-existent source port
            var invalidSourcePort =
                stateService.IsValidConnection("source-node", "non-existent-port", "target-node", "input");
            Assert.False(invalidSourcePort, "Connection from non-existent source port should be rejected");

            // Act & Assert - Non-existent target port
            var invalidTargetPort =
                stateService.IsValidConnection("source-node", "output", "target-node", "non-existent-port");
            Assert.False(invalidTargetPort, "Connection to non-existent target port should be rejected");
        }, iter: 100);
    }

    #region Helpers

    /// <summary>Creates a WorkflowStateService with two nodes having specified port types.</summary>
    private static WorkflowStateService CreateStateServiceWithNodes(PortType sourceOutputType, PortType targetInputType)
    {
        var stateService = new WorkflowStateService();

        // Create node definitions with specified port types
        var sourceDefinition = new NodeDefinition
        {
            Type = "source-type",
            Name = "Source Node",
            Description = "Test source node",
            Category = NodeCategory.Trigger,
            Icon = "▶",
            Inputs = [new PortDefinition { Name = "input", DisplayName = "Input", Type = PortType.Any }],
            Outputs = [new PortDefinition { Name = "output", DisplayName = "Output", Type = sourceOutputType }]
        };

        var targetDefinition = new NodeDefinition
        {
            Type = "target-type",
            Name = "Target Node",
            Description = "Test target node",
            Category = NodeCategory.Action,
            Icon = "●",
            Inputs = [new PortDefinition { Name = "input", DisplayName = "Input", Type = targetInputType }],
            Outputs = [new PortDefinition { Name = "output", DisplayName = "Output", Type = PortType.Any }]
        };

        stateService.SetNodeDefinitions([sourceDefinition, targetDefinition]);

        // Add nodes to the workflow
        var sourceNode = new WorkflowNode
        {
            Id = "source-node",
            Type = "source-type",
            Name = "Source",
            Position = new Position(0, 0),
            Configuration = JsonDocument.Parse("{}").RootElement
        };

        var targetNode = new WorkflowNode
        {
            Id = "target-node",
            Type = "target-type",
            Name = "Target",
            Position = new Position(200, 0),
            Configuration = JsonDocument.Parse("{}").RootElement
        };

        stateService.AddNode(sourceNode);
        stateService.AddNode(targetNode);

        return stateService;
    }

    /// <summary>Creates a WorkflowStateService with a single node for self-connection testing.</summary>
    private static WorkflowStateService CreateStateServiceWithSingleNode(PortType outputType, PortType inputType)
    {
        var stateService = new WorkflowStateService();

        var nodeDefinition = new NodeDefinition
        {
            Type = "single-type",
            Name = "Single Node",
            Description = "Test node for self-connection",
            Category = NodeCategory.Logic,
            Icon = "⑂",
            Inputs = [new PortDefinition { Name = "input", DisplayName = "Input", Type = inputType }],
            Outputs = [new PortDefinition { Name = "output", DisplayName = "Output", Type = outputType }]
        };

        stateService.SetNodeDefinitions([nodeDefinition]);

        var node = new WorkflowNode
        {
            Id = "single-node",
            Type = "single-type",
            Name = "Single",
            Position = new Position(0, 0),
            Configuration = JsonDocument.Parse("{}").RootElement
        };

        stateService.AddNode(node);

        return stateService;
    }

    #endregion

    #region Generators

    /// <summary>Generator for all PortType values.</summary>
    private static readonly Gen<PortType> GenPortType =
        Gen.OneOfConst(
            PortType.Any,
            PortType.Object,
            PortType.Array,
            PortType.String,
            PortType.Number,
            PortType.Boolean
        );

    /// <summary>Generator for specific (non-Any) PortType values.</summary>
    private static readonly Gen<PortType> GenSpecificPortType =
        Gen.OneOfConst(
            PortType.Object,
            PortType.Array,
            PortType.String,
            PortType.Number,
            PortType.Boolean
        );

    /// <summary>Generator for pairs of port types.</summary>
    private static readonly Gen<(PortType Source, PortType Target)> GenPortTypePair =
        from source in GenPortType
        from target in GenPortType
        select (source, target);

    /// <summary>Generator for incompatible port type pairs (specific types that don't match).</summary>
    private static readonly Gen<(PortType Source, PortType Target)> GenIncompatiblePortTypePair =
        from source in GenSpecificPortType
        from target in GenSpecificPortType
        where !AreTypesCompatible(source, target)
        select (source, target);

    /// <summary>Checks if two port types are compatible according to the rules.</summary>
    private static bool AreTypesCompatible(PortType source, PortType target)
    {
        // Any is compatible with everything
        if (source == PortType.Any || target == PortType.Any)
            return true;

        // Same types are compatible
        if (source == target)
            return true;

        // Object source is compatible with all targets (loose typing)
        if (source == PortType.Object)
            return true;

        // Array can connect to Object
        if (source == PortType.Array && target == PortType.Object)
            return true;

        return false;
    }

    #endregion
}
