using CsCheck;
using Vyshyvanka.Designer.Services;
using Vyshyvanka.Tests.Integration.Designer.Generators;

namespace Vyshyvanka.Tests.Integration.Designer;

/// <summary>
/// Integration tests for WorkflowStateService node operations.
/// Tests node addition, removal, and position updates.
/// </summary>
public class WorkflowStateServiceNodeTests
{
    #region Node Addition Tests (Task 2.1)

    /// <summary>
    /// Tests that when a node is added to the workflow, it appears in the node collection.
    /// Validates: Requirements 1.1
    /// </summary>
    [Fact]
    public void WhenNodeAddedThenNodeAppearsInCollection()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node = TestFixtures.CreateTriggerNode();

        // Act
        service.AddNode(node);

        // Assert
        Assert.Contains(service.Workflow.Nodes, n => n.Id == node.Id);
        Assert.Single(service.Workflow.Nodes);
    }

    /// <summary>
    /// Tests that when a node is added to the workflow, it becomes selected.
    /// Validates: Requirements 1.1
    /// </summary>
    [Fact]
    public void WhenNodeAddedThenNodeIsSelected()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node = TestFixtures.CreateTriggerNode();

        // Act
        service.AddNode(node);

        // Assert
        Assert.Equal(node.Id, service.SelectedNodeId);
    }

    /// <summary>
    /// Tests that when a node is added to the workflow, the dirty flag is set.
    /// Validates: Requirements 1.1
    /// </summary>
    [Fact]
    public void WhenNodeAddedThenDirtyFlagIsSet()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node = TestFixtures.CreateTriggerNode();
        Assert.False(service.IsDirty);

        // Act
        service.AddNode(node);

        // Assert
        Assert.True(service.IsDirty);
    }

    /// <summary>
    /// Tests that when multiple nodes are added, all appear in the collection.
    /// Validates: Requirements 1.1
    /// </summary>
    [Fact]
    public void WhenMultipleNodesAddedThenAllNodesAppearInCollection()
    {
        // Arrange
        var service = new WorkflowStateService();
        var triggerNode = TestFixtures.CreateTriggerNode();
        var actionNode = TestFixtures.CreateHttpRequestNode();

        // Act
        service.AddNode(triggerNode);
        service.AddNode(actionNode);

        // Assert
        Assert.Equal(2, service.Workflow.Nodes.Count);
        Assert.Contains(service.Workflow.Nodes, n => n.Id == triggerNode.Id);
        Assert.Contains(service.Workflow.Nodes, n => n.Id == actionNode.Id);
    }

    /// <summary>
    /// Tests that when a node is added, the state change event is raised.
    /// Validates: Requirements 1.1
    /// </summary>
    [Fact]
    public void WhenNodeAddedThenStateChangeEventIsRaised()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node = TestFixtures.CreateTriggerNode();
        var eventRaised = false;
        service.OnStateChanged += () => eventRaised = true;

        // Act
        service.AddNode(node);

        // Assert
        Assert.True(eventRaised);
    }

    #endregion


    #region Node Removal Tests (Task 2.3)

    /// <summary>
    /// Tests that when a node is removed, it is gone from the collection.
    /// Validates: Requirements 1.2
    /// </summary>
    [Fact]
    public void WhenNodeRemovedThenNodeIsGoneFromCollection()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node = TestFixtures.CreateTriggerNode();
        service.AddNode(node);
        Assert.Single(service.Workflow.Nodes);

        // Act
        service.RemoveNode(node.Id);

        // Assert
        Assert.Empty(service.Workflow.Nodes);
        Assert.DoesNotContain(service.Workflow.Nodes, n => n.Id == node.Id);
    }

    /// <summary>
    /// Tests that when a node is removed, connections to/from that node are also removed.
    /// Validates: Requirements 1.2
    /// </summary>
    [Fact]
    public void WhenNodeRemovedThenConnectionsToFromNodeAreRemoved()
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

        // Act
        service.RemoveNode(actionNode.Id);

        // Assert
        Assert.Empty(service.Workflow.Connections);
    }

    /// <summary>
    /// Tests that when a selected node is removed, the selection is cleared.
    /// Validates: Requirements 1.2
    /// </summary>
    [Fact]
    public void WhenSelectedNodeRemovedThenSelectionIsCleared()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node = TestFixtures.CreateTriggerNode();
        service.AddNode(node);
        Assert.Equal(node.Id, service.SelectedNodeId);

        // Act
        service.RemoveNode(node.Id);

        // Assert
        Assert.Null(service.SelectedNodeId);
    }

    /// <summary>
    /// Tests that when a non-selected node is removed, the selection remains unchanged.
    /// Validates: Requirements 1.2
    /// </summary>
    [Fact]
    public void WhenNonSelectedNodeRemovedThenSelectionRemainsUnchanged()
    {
        // Arrange
        var service = new WorkflowStateService();
        var triggerNode = TestFixtures.CreateTriggerNode();
        var actionNode = TestFixtures.CreateHttpRequestNode();
        service.AddNode(triggerNode);
        service.AddNode(actionNode);
        // actionNode is now selected (last added)
        service.SelectNode(triggerNode.Id);
        Assert.Equal(triggerNode.Id, service.SelectedNodeId);

        // Act
        service.RemoveNode(actionNode.Id);

        // Assert
        Assert.Equal(triggerNode.Id, service.SelectedNodeId);
    }

    /// <summary>
    /// Tests that when a node is removed, the dirty flag is set.
    /// Validates: Requirements 1.2
    /// </summary>
    [Fact]
    public void WhenNodeRemovedThenDirtyFlagIsSet()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node = TestFixtures.CreateTriggerNode();
        service.AddNode(node);
        service.MarkAsSaved();
        Assert.False(service.IsDirty);

        // Act
        service.RemoveNode(node.Id);

        // Assert
        Assert.True(service.IsDirty);
    }

    #endregion

    #region Node Position Update Tests (Task 2.5)

    /// <summary>
    /// Tests that when a node's position is updated, the coordinates are changed.
    /// Validates: Requirements 1.3
    /// </summary>
    [Fact]
    public void WhenNodePositionUpdatedThenCoordinatesAreChanged()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node = TestFixtures.CreateTriggerNode();
        service.AddNode(node);
        var newX = 500.0;
        var newY = 600.0;

        // Act
        service.MoveNode(node.Id, newX, newY);

        // Assert
        var updatedNode = service.GetNode(node.Id);
        Assert.NotNull(updatedNode);
        Assert.Equal(newX, updatedNode.Position.X);
        Assert.Equal(newY, updatedNode.Position.Y);
    }

    /// <summary>
    /// Tests that when a node's position is updated, the state change event is raised.
    /// Validates: Requirements 1.3
    /// </summary>
    [Fact]
    public void WhenNodePositionUpdatedThenStateChangeEventIsRaised()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node = TestFixtures.CreateTriggerNode();
        service.AddNode(node);
        var eventRaised = false;
        service.OnStateChanged += () => eventRaised = true;

        // Act
        service.MoveNode(node.Id, 500.0, 600.0);

        // Assert
        Assert.True(eventRaised);
    }

    /// <summary>
    /// Tests that moving a non-existent node does not throw an exception.
    /// Validates: Requirements 1.3
    /// </summary>
    [Fact]
    public void WhenMovingNonExistentNodeThenNoExceptionThrown()
    {
        // Arrange
        var service = new WorkflowStateService();

        // Act & Assert - should not throw
        var exception = Record.Exception(() => service.MoveNode("non-existent-id", 100.0, 200.0));
        Assert.Null(exception);
    }

    #endregion

    #region Property Tests

    /// <summary>
    /// Feature: blazor-integration-tests, Property 1: Node Addition Invariant
    /// For any valid workflow node, when added to the WorkflowStateService, 
    /// the node SHALL appear in the workflow's node collection and be selected.
    /// Validates: Requirements 1.1
    /// </summary>
    [Fact]
    public void Property1_NodeAdditionInvariant()
    {
        DesignerGenerators.NodeGen.Sample(node =>
        {
            // Arrange
            var service = new WorkflowStateService();

            // Act
            service.AddNode(node);

            // Assert - Node appears in collection
            Assert.Contains(service.Workflow.Nodes, n => n.Id == node.Id);
            
            // Assert - Node is selected
            Assert.Equal(node.Id, service.SelectedNodeId);
            
            // Assert - Dirty flag is set
            Assert.True(service.IsDirty);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: blazor-integration-tests, Property 2: Node Removal Cascades Connections
    /// For any workflow with nodes and connections, when a node is removed, 
    /// the node and all connections referencing that node SHALL be removed from the workflow.
    /// Validates: Requirements 1.2
    /// </summary>
    [Fact]
    public void Property2_NodeRemovalCascadesConnections()
    {
        DesignerGenerators.ValidWorkflowGen.Sample(workflow =>
        {
            // Skip if workflow has fewer than 2 nodes (no connections to test)
            if (workflow.Nodes.Count < 2)
                return;

            // Arrange
            var service = new WorkflowStateService();
            service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
            service.LoadWorkflow(workflow);
            
            // Pick a node to remove (not the first one to ensure we have connections)
            var nodeToRemove = workflow.Nodes[^1];
            var nodeId = nodeToRemove.Id;

            // Act
            service.RemoveNode(nodeId);

            // Assert - Node is removed
            Assert.DoesNotContain(service.Workflow.Nodes, n => n.Id == nodeId);
            
            // Assert - No connections reference the removed node
            Assert.DoesNotContain(service.Workflow.Connections, 
                c => c.SourceNodeId == nodeId || c.TargetNodeId == nodeId);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: blazor-integration-tests, Property 3: Node Position Update
    /// For any node in a workflow and any valid position coordinates, 
    /// updating the node's position SHALL result in the node having the new coordinates.
    /// Validates: Requirements 1.3
    /// </summary>
    [Fact]
    public void Property3_NodePositionUpdate()
    {
        var testGen = from node in DesignerGenerators.NodeGen
                      from newPosition in DesignerGenerators.PositionGen
                      select (node, newPosition);

        testGen.Sample(data =>
        {
            var (node, newPosition) = data;
            
            // Arrange
            var service = new WorkflowStateService();
            service.AddNode(node);

            // Act
            service.MoveNode(node.Id, newPosition.X, newPosition.Y);

            // Assert
            var updatedNode = service.GetNode(node.Id);
            Assert.NotNull(updatedNode);
            Assert.Equal(newPosition.X, updatedNode.Position.X);
            Assert.Equal(newPosition.Y, updatedNode.Position.Y);
        }, iter: 100);
    }

    #endregion
}
