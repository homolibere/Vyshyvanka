using CsCheck;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using FlowForge.Designer.Services;
using FlowForge.Tests.Integration.Designer.Generators;

namespace FlowForge.Tests.Integration.Designer;

/// <summary>
/// Integration tests for WorkflowStateService connection operations.
/// Tests connection addition, removal, duplicate prevention, and validation.
/// </summary>
public class WorkflowStateServiceConnectionTests
{
    #region Connection Addition Tests (Task 4.1)

    /// <summary>
    /// Tests that when a valid connection is added, it appears in the connections collection.
    /// Validates: Requirements 2.1
    /// </summary>
    [Fact]
    public void WhenValidConnectionAddedThenConnectionAppearsInCollection()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        
        var triggerNode = TestFixtures.CreateTriggerNode();
        var actionNode = TestFixtures.CreateHttpRequestNode();
        service.AddNode(triggerNode);
        service.AddNode(actionNode);
        
        var connection = TestFixtures.CreateConnection(triggerNode.Id, actionNode.Id);

        // Act
        service.AddConnection(connection);

        // Assert
        Assert.Single(service.Workflow.Connections);
        Assert.Contains(service.Workflow.Connections, c =>
            c.SourceNodeId == connection.SourceNodeId &&
            c.TargetNodeId == connection.TargetNodeId);
    }

    /// <summary>
    /// Tests that when a connection is added, the dirty flag is set.
    /// Validates: Requirements 2.1
    /// </summary>
    [Fact]
    public void WhenConnectionAddedThenDirtyFlagIsSet()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        
        var triggerNode = TestFixtures.CreateTriggerNode();
        var actionNode = TestFixtures.CreateHttpRequestNode();
        service.AddNode(triggerNode);
        service.AddNode(actionNode);
        service.MarkAsSaved();
        Assert.False(service.IsDirty);
        
        var connection = TestFixtures.CreateConnection(triggerNode.Id, actionNode.Id);

        // Act
        service.AddConnection(connection);

        // Assert
        Assert.True(service.IsDirty);
    }

    /// <summary>
    /// Tests that when a connection is added, the state change event is raised.
    /// Validates: Requirements 2.1
    /// </summary>
    [Fact]
    public void WhenConnectionAddedThenStateChangeEventIsRaised()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        
        var triggerNode = TestFixtures.CreateTriggerNode();
        var actionNode = TestFixtures.CreateHttpRequestNode();
        service.AddNode(triggerNode);
        service.AddNode(actionNode);
        
        var eventRaised = false;
        service.OnStateChanged += () => eventRaised = true;
        
        var connection = TestFixtures.CreateConnection(triggerNode.Id, actionNode.Id);

        // Act
        service.AddConnection(connection);

        // Assert
        Assert.True(eventRaised);
    }

    /// <summary>
    /// Tests that multiple connections can be added between different nodes.
    /// Validates: Requirements 2.1
    /// </summary>
    [Fact]
    public void WhenMultipleConnectionsAddedThenAllConnectionsAppearInCollection()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        
        var triggerNode = TestFixtures.CreateTriggerNode();
        var actionNode1 = TestFixtures.CreateHttpRequestNode("action1");
        var actionNode2 = TestFixtures.CreateHttpRequestNode("action2");
        service.AddNode(triggerNode);
        service.AddNode(actionNode1);
        service.AddNode(actionNode2);
        
        var connection1 = TestFixtures.CreateConnection(triggerNode.Id, actionNode1.Id);
        var connection2 = TestFixtures.CreateConnection(actionNode1.Id, actionNode2.Id);

        // Act
        service.AddConnection(connection1);
        service.AddConnection(connection2);

        // Assert
        Assert.Equal(2, service.Workflow.Connections.Count);
    }

    #endregion

    #region Duplicate Connection Prevention Tests (Task 4.3)

    /// <summary>
    /// Tests that when a duplicate connection is added, it is not added to the collection.
    /// Validates: Requirements 2.2
    /// </summary>
    [Fact]
    public void WhenDuplicateConnectionAddedThenConnectionIsNotAdded()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        
        var triggerNode = TestFixtures.CreateTriggerNode();
        var actionNode = TestFixtures.CreateHttpRequestNode();
        service.AddNode(triggerNode);
        service.AddNode(actionNode);
        
        var connection = TestFixtures.CreateConnection(triggerNode.Id, actionNode.Id);
        service.AddConnection(connection);
        Assert.Single(service.Workflow.Connections);

        // Act - try to add the same connection again
        var duplicateConnection = TestFixtures.CreateConnection(triggerNode.Id, actionNode.Id);
        service.AddConnection(duplicateConnection);

        // Assert - count should still be 1
        Assert.Single(service.Workflow.Connections);
    }

    /// <summary>
    /// Tests that when a duplicate connection is added, the connection count remains unchanged.
    /// Validates: Requirements 2.2
    /// </summary>
    [Fact]
    public void WhenDuplicateConnectionAddedThenConnectionCountRemainsUnchanged()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        
        var triggerNode = TestFixtures.CreateTriggerNode();
        var actionNode = TestFixtures.CreateHttpRequestNode();
        service.AddNode(triggerNode);
        service.AddNode(actionNode);
        
        var connection = TestFixtures.CreateConnection(triggerNode.Id, actionNode.Id);
        service.AddConnection(connection);
        var initialCount = service.Workflow.Connections.Count;

        // Act - try to add the same connection multiple times
        for (int i = 0; i < 5; i++)
        {
            var duplicateConnection = TestFixtures.CreateConnection(triggerNode.Id, actionNode.Id);
            service.AddConnection(duplicateConnection);
        }

        // Assert
        Assert.Equal(initialCount, service.Workflow.Connections.Count);
    }

    /// <summary>
    /// Tests that connections with different ports are not considered duplicates.
    /// Validates: Requirements 2.2
    /// </summary>
    [Fact]
    public void WhenConnectionWithDifferentPortsAddedThenConnectionIsAdded()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        
        var triggerNode = TestFixtures.CreateTriggerNode();
        var ifNode = TestFixtures.CreateIfNode();
        var actionNode1 = TestFixtures.CreateHttpRequestNode("action1");
        var actionNode2 = TestFixtures.CreateHttpRequestNode("action2");
        service.AddNode(triggerNode);
        service.AddNode(ifNode);
        service.AddNode(actionNode1);
        service.AddNode(actionNode2);
        
        // Add connection from trigger to if
        service.AddConnection(TestFixtures.CreateConnection(triggerNode.Id, ifNode.Id));
        
        // Add connections from if node's different output ports
        var connectionTrue = TestFixtures.CreateConnection(ifNode.Id, actionNode1.Id, "true", "input");
        var connectionFalse = TestFixtures.CreateConnection(ifNode.Id, actionNode2.Id, "false", "input");

        // Act
        service.AddConnection(connectionTrue);
        service.AddConnection(connectionFalse);

        // Assert - should have 3 connections total
        Assert.Equal(3, service.Workflow.Connections.Count);
    }

    #endregion

    #region Connection Validation Tests (Task 4.5)

    /// <summary>
    /// Tests that self-connections are rejected.
    /// Validates: Requirements 2.4
    /// </summary>
    [Fact]
    public void WhenSelfConnectionAttemptedThenConnectionIsRejected()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        
        var actionNode = TestFixtures.CreateHttpRequestNode();
        service.AddNode(actionNode);

        // Act
        var isValid = service.IsValidConnection(actionNode.Id, "output", actionNode.Id, "input");

        // Assert
        Assert.False(isValid);
    }

    /// <summary>
    /// Tests that connections between compatible port types are accepted.
    /// Validates: Requirements 2.5
    /// </summary>
    [Fact]
    public void WhenCompatiblePortTypesConnectedThenConnectionIsValid()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        
        var triggerNode = TestFixtures.CreateTriggerNode();
        var actionNode = TestFixtures.CreateHttpRequestNode();
        service.AddNode(triggerNode);
        service.AddNode(actionNode);

        // Act - Any to Any should be compatible
        var isValid = service.IsValidConnection(triggerNode.Id, "output", actionNode.Id, "input");

        // Assert
        Assert.True(isValid);
    }

    /// <summary>
    /// Tests that connections to non-existent nodes are rejected.
    /// Validates: Requirements 2.4
    /// </summary>
    [Fact]
    public void WhenConnectionToNonExistentNodeAttemptedThenConnectionIsRejected()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        
        var triggerNode = TestFixtures.CreateTriggerNode();
        service.AddNode(triggerNode);

        // Act
        var isValid = service.IsValidConnection(triggerNode.Id, "output", "non-existent-id", "input");

        // Assert
        Assert.False(isValid);
    }

    /// <summary>
    /// Tests that connections from non-existent ports are rejected.
    /// Validates: Requirements 2.5
    /// </summary>
    [Fact]
    public void WhenConnectionFromNonExistentPortAttemptedThenConnectionIsRejected()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        
        var triggerNode = TestFixtures.CreateTriggerNode();
        var actionNode = TestFixtures.CreateHttpRequestNode();
        service.AddNode(triggerNode);
        service.AddNode(actionNode);

        // Act
        var isValid = service.IsValidConnection(triggerNode.Id, "non-existent-port", actionNode.Id, "input");

        // Assert
        Assert.False(isValid);
    }

    #endregion


    #region Property Tests

    /// <summary>
    /// Feature: blazor-integration-tests, Property 4: Connection Addition
    /// For any valid connection between two different nodes, adding the connection 
    /// SHALL result in the connection appearing in the workflow's connection collection.
    /// Validates: Requirements 2.1
    /// </summary>
    [Fact]
    public void Property4_ConnectionAddition()
    {
        // Generate pairs of nodes and create connections between them
        var testGen = from triggerNode in DesignerGenerators.TriggerNodeGen
                      from actionNode in DesignerGenerators.ActionNodeGen
                      select (triggerNode, actionNode);

        testGen.Sample(data =>
        {
            var (triggerNode, actionNode) = data;
            
            // Ensure nodes have different IDs
            if (triggerNode.Id == actionNode.Id)
                return;

            // Arrange
            var service = new WorkflowStateService();
            service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
            service.AddNode(triggerNode);
            service.AddNode(actionNode);
            
            var connection = new Connection
            {
                SourceNodeId = triggerNode.Id,
                SourcePort = "output",
                TargetNodeId = actionNode.Id,
                TargetPort = "input"
            };

            // Act
            service.AddConnection(connection);

            // Assert - Connection appears in collection
            Assert.Contains(service.Workflow.Connections, c =>
                c.SourceNodeId == connection.SourceNodeId &&
                c.SourcePort == connection.SourcePort &&
                c.TargetNodeId == connection.TargetNodeId &&
                c.TargetPort == connection.TargetPort);
            
            // Assert - Dirty flag is set
            Assert.True(service.IsDirty);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: blazor-integration-tests, Property 5: Duplicate Connection Prevention
    /// For any connection, adding the same connection twice SHALL result in 
    /// only one instance of the connection in the workflow.
    /// Validates: Requirements 2.2
    /// </summary>
    [Fact]
    public void Property5_DuplicateConnectionPrevention()
    {
        var testGen = from triggerNode in DesignerGenerators.TriggerNodeGen
                      from actionNode in DesignerGenerators.ActionNodeGen
                      from addCount in Gen.Int[2, 10]
                      select (triggerNode, actionNode, addCount);

        testGen.Sample(data =>
        {
            var (triggerNode, actionNode, addCount) = data;
            
            // Ensure nodes have different IDs
            if (triggerNode.Id == actionNode.Id)
                return;

            // Arrange
            var service = new WorkflowStateService();
            service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
            service.AddNode(triggerNode);
            service.AddNode(actionNode);

            // Act - Add the same connection multiple times
            for (int i = 0; i < addCount; i++)
            {
                var connection = new Connection
                {
                    SourceNodeId = triggerNode.Id,
                    SourcePort = "output",
                    TargetNodeId = actionNode.Id,
                    TargetPort = "input"
                };
                service.AddConnection(connection);
            }

            // Assert - Only one connection should exist
            var matchingConnections = service.Workflow.Connections.Count(c =>
                c.SourceNodeId == triggerNode.Id &&
                c.SourcePort == "output" &&
                c.TargetNodeId == actionNode.Id &&
                c.TargetPort == "input");
            
            Assert.Equal(1, matchingConnections);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: blazor-integration-tests, Property 6: Self-Connection Rejection
    /// For any node, attempting to create a connection from the node to itself 
    /// SHALL be rejected as invalid.
    /// Validates: Requirements 2.4
    /// </summary>
    [Fact]
    public void Property6_SelfConnectionRejection()
    {
        DesignerGenerators.ActionNodeGen.Sample(node =>
        {
            // Arrange
            var service = new WorkflowStateService();
            service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
            service.AddNode(node);

            // Act
            var isValid = service.IsValidConnection(node.Id, "output", node.Id, "input");

            // Assert - Self-connection should always be invalid
            Assert.False(isValid);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: blazor-integration-tests, Property 7: Port Type Compatibility
    /// For any two port types, the compatibility check SHALL return true if and only if 
    /// the types are compatible according to the type rules.
    /// Validates: Requirements 2.5
    /// </summary>
    [Fact]
    public void Property7_PortTypeCompatibility()
    {
        var testGen = from sourceType in DesignerGenerators.PortTypeGen
                      from targetType in DesignerGenerators.PortTypeGen
                      select (sourceType, targetType);

        testGen.Sample(data =>
        {
            var (sourceType, targetType) = data;
            
            // Calculate expected compatibility based on rules:
            // 1. Any type is compatible with everything
            // 2. Same types are always compatible
            // 3. Object can connect to most types (loose typing)
            // 4. Array can connect to Object
            var expectedCompatible = 
                sourceType == PortType.Any || 
                targetType == PortType.Any ||
                sourceType == targetType ||
                sourceType == PortType.Object ||
                (sourceType == PortType.Array && targetType == PortType.Object);

            // Arrange - Create nodes with specific port types
            var service = new WorkflowStateService();
            
            // Create custom node definitions with specific port types
            var sourceNodeDef = new NodeDefinition
            {
                Type = "SourceNode",
                Name = "Source Node",
                Description = "Test source node",
                Category = NodeCategory.Action,
                Icon = "test",
                Inputs = [new PortDefinition { Name = "input", DisplayName = "Input", Type = PortType.Any, IsRequired = true }],
                Outputs = [new PortDefinition { Name = "output", DisplayName = "Output", Type = sourceType, IsRequired = false }]
            };
            
            var targetNodeDef = new NodeDefinition
            {
                Type = "TargetNode",
                Name = "Target Node",
                Description = "Test target node",
                Category = NodeCategory.Action,
                Icon = "test",
                Inputs = [new PortDefinition { Name = "input", DisplayName = "Input", Type = targetType, IsRequired = true }],
                Outputs = [new PortDefinition { Name = "output", DisplayName = "Output", Type = PortType.Any, IsRequired = false }]
            };
            
            service.SetNodeDefinitions([sourceNodeDef, targetNodeDef]);
            
            var sourceNode = new WorkflowNode
            {
                Id = Guid.NewGuid().ToString(),
                Type = "SourceNode",
                Name = "Source",
                Position = new Position(100, 100)
            };
            
            var targetNode = new WorkflowNode
            {
                Id = Guid.NewGuid().ToString(),
                Type = "TargetNode",
                Name = "Target",
                Position = new Position(300, 100)
            };
            
            service.AddNode(sourceNode);
            service.AddNode(targetNode);

            // Act
            var isValid = service.IsValidConnection(sourceNode.Id, "output", targetNode.Id, "input");

            // Assert
            Assert.Equal(expectedCompatible, isValid);
        }, iter: 100);
    }

    #endregion
}
