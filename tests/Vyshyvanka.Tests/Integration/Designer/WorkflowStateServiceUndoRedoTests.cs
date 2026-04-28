using CsCheck;
using Vyshyvanka.Designer.Services;
using Vyshyvanka.Tests.Integration.Designer.Generators;

namespace Vyshyvanka.Tests.Integration.Designer;

/// <summary>
/// Integration tests for WorkflowStateService undo/redo operations.
/// Tests undo restores previous state, redo restores undone state, and redo stack clearing.
/// </summary>
public class WorkflowStateServiceUndoRedoTests
{
    #region Undo Tests (Task 5.1)

    /// <summary>
    /// Tests that undo is not available on a fresh service.
    /// Validates: Requirements 3.1
    /// </summary>
    [Fact]
    public void WhenServiceIsNewThenCanUndoIsFalse()
    {
        // Arrange
        var service = new WorkflowStateService();

        // Assert
        Assert.False(service.CanUndo);
    }

    /// <summary>
    /// Tests that after adding a node, undo is available.
    /// Validates: Requirements 3.1
    /// </summary>
    [Fact]
    public void WhenNodeAddedThenCanUndoIsTrue()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node = TestFixtures.CreateTriggerNode();

        // Act
        service.AddNode(node);

        // Assert
        Assert.True(service.CanUndo);
    }

    /// <summary>
    /// Tests that undo restores previous state after adding a node.
    /// Validates: Requirements 3.1, 3.2
    /// </summary>
    [Fact]
    public void WhenUndoAfterAddNodeThenNodeIsRemoved()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node = TestFixtures.CreateTriggerNode();
        service.AddNode(node);
        Assert.Single(service.Workflow.Nodes);

        // Act
        service.Undo();

        // Assert
        Assert.Empty(service.Workflow.Nodes);
    }


    /// <summary>
    /// Tests that undo restores previous state after removing a node.
    /// Validates: Requirements 3.1, 3.2
    /// </summary>
    [Fact]
    public void WhenUndoAfterRemoveNodeThenNodeIsRestored()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node = TestFixtures.CreateTriggerNode();
        service.AddNode(node);
        service.RemoveNode(node.Id);
        Assert.Empty(service.Workflow.Nodes);

        // Act
        service.Undo();

        // Assert
        Assert.Single(service.Workflow.Nodes);
        Assert.Contains(service.Workflow.Nodes, n => n.Id == node.Id);
    }

    /// <summary>
    /// Tests that undo restores previous state after adding a connection.
    /// Validates: Requirements 3.1, 3.2
    /// </summary>
    [Fact]
    public void WhenUndoAfterAddConnectionThenConnectionIsRemoved()
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
        service.Undo();

        // Assert
        Assert.Empty(service.Workflow.Connections);
    }

    /// <summary>
    /// Tests that multiple undo operations work correctly.
    /// Validates: Requirements 3.1, 3.2
    /// </summary>
    [Fact]
    public void WhenMultipleUndosThenStatesAreRestoredInOrder()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node1 = TestFixtures.CreateTriggerNode("node1");
        var node2 = TestFixtures.CreateHttpRequestNode("node2");
        
        service.AddNode(node1);
        service.AddNode(node2);
        Assert.Equal(2, service.Workflow.Nodes.Count);

        // Act & Assert - First undo removes node2
        service.Undo();
        Assert.Single(service.Workflow.Nodes);
        Assert.Contains(service.Workflow.Nodes, n => n.Id == "node1");
        Assert.DoesNotContain(service.Workflow.Nodes, n => n.Id == "node2");

        // Act & Assert - Second undo removes node1
        service.Undo();
        Assert.Empty(service.Workflow.Nodes);
    }

    /// <summary>
    /// Tests that undo raises state change event.
    /// Validates: Requirements 3.2
    /// </summary>
    [Fact]
    public void WhenUndoThenStateChangeEventIsRaised()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node = TestFixtures.CreateTriggerNode();
        service.AddNode(node);
        
        var eventRaised = false;
        service.OnStateChanged += () => eventRaised = true;

        // Act
        service.Undo();

        // Assert
        Assert.True(eventRaised);
    }

    /// <summary>
    /// Tests that undo when stack is empty does nothing.
    /// Validates: Requirements 3.2
    /// </summary>
    [Fact]
    public void WhenUndoWithEmptyStackThenNoExceptionThrown()
    {
        // Arrange
        var service = new WorkflowStateService();

        // Act & Assert - should not throw
        var exception = Record.Exception(() => service.Undo());
        Assert.Null(exception);
    }

    #endregion

    #region Redo Tests (Task 5.1)

    /// <summary>
    /// Tests that redo is not available on a fresh service.
    /// Validates: Requirements 3.3
    /// </summary>
    [Fact]
    public void WhenServiceIsNewThenCanRedoIsFalse()
    {
        // Arrange
        var service = new WorkflowStateService();

        // Assert
        Assert.False(service.CanRedo);
    }

    /// <summary>
    /// Tests that after undo, redo is available.
    /// Validates: Requirements 3.3
    /// </summary>
    [Fact]
    public void WhenUndoPerformedThenCanRedoIsTrue()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node = TestFixtures.CreateTriggerNode();
        service.AddNode(node);
        service.Undo();

        // Assert
        Assert.True(service.CanRedo);
    }

    /// <summary>
    /// Tests that redo restores undone state.
    /// Validates: Requirements 3.3
    /// </summary>
    [Fact]
    public void WhenRedoAfterUndoThenStateIsRestored()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node = TestFixtures.CreateTriggerNode();
        service.AddNode(node);
        var nodeCountAfterAdd = service.Workflow.Nodes.Count;
        service.Undo();
        Assert.Empty(service.Workflow.Nodes);

        // Act
        service.Redo();

        // Assert
        Assert.Equal(nodeCountAfterAdd, service.Workflow.Nodes.Count);
        Assert.Contains(service.Workflow.Nodes, n => n.Id == node.Id);
    }


    /// <summary>
    /// Tests that multiple redo operations work correctly.
    /// Validates: Requirements 3.3
    /// </summary>
    [Fact]
    public void WhenMultipleRedosThenStatesAreRestoredInOrder()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node1 = TestFixtures.CreateTriggerNode("node1");
        var node2 = TestFixtures.CreateHttpRequestNode("node2");
        
        service.AddNode(node1);
        service.AddNode(node2);
        service.Undo();
        service.Undo();
        Assert.Empty(service.Workflow.Nodes);

        // Act & Assert - First redo restores node1
        service.Redo();
        Assert.Single(service.Workflow.Nodes);
        Assert.Contains(service.Workflow.Nodes, n => n.Id == "node1");

        // Act & Assert - Second redo restores node2
        service.Redo();
        Assert.Equal(2, service.Workflow.Nodes.Count);
        Assert.Contains(service.Workflow.Nodes, n => n.Id == "node2");
    }

    /// <summary>
    /// Tests that redo raises state change event.
    /// Validates: Requirements 3.3
    /// </summary>
    [Fact]
    public void WhenRedoThenStateChangeEventIsRaised()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node = TestFixtures.CreateTriggerNode();
        service.AddNode(node);
        service.Undo();
        
        var eventRaised = false;
        service.OnStateChanged += () => eventRaised = true;

        // Act
        service.Redo();

        // Assert
        Assert.True(eventRaised);
    }

    /// <summary>
    /// Tests that redo when stack is empty does nothing.
    /// Validates: Requirements 3.3
    /// </summary>
    [Fact]
    public void WhenRedoWithEmptyStackThenNoExceptionThrown()
    {
        // Arrange
        var service = new WorkflowStateService();

        // Act & Assert - should not throw
        var exception = Record.Exception(() => service.Redo());
        Assert.Null(exception);
    }

    #endregion

    #region Redo Stack Clearing Tests (Task 5.1)

    /// <summary>
    /// Tests that redo stack is cleared when a new action is performed after undo.
    /// Validates: Requirements 3.4
    /// </summary>
    [Fact]
    public void WhenNewActionAfterUndoThenRedoStackIsCleared()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node1 = TestFixtures.CreateTriggerNode("node1");
        var node2 = TestFixtures.CreateHttpRequestNode("node2");
        
        service.AddNode(node1);
        service.Undo();
        Assert.True(service.CanRedo);

        // Act - perform a new action
        service.AddNode(node2);

        // Assert - redo should no longer be available
        Assert.False(service.CanRedo);
    }

    /// <summary>
    /// Tests that redo stack is cleared when adding a connection after undo.
    /// Validates: Requirements 3.4
    /// </summary>
    [Fact]
    public void WhenAddConnectionAfterUndoThenRedoStackIsCleared()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        
        var triggerNode = TestFixtures.CreateTriggerNode();
        var actionNode = TestFixtures.CreateHttpRequestNode();
        service.AddNode(triggerNode);
        service.AddNode(actionNode);
        service.Undo();
        Assert.True(service.CanRedo);

        // Act - add a connection (new action)
        service.AddNode(TestFixtures.CreateEmailSendNode());

        // Assert - redo should no longer be available
        Assert.False(service.CanRedo);
    }

    /// <summary>
    /// Tests that redo stack is cleared when removing a node after undo.
    /// Validates: Requirements 3.4
    /// </summary>
    [Fact]
    public void WhenRemoveNodeAfterUndoThenRedoStackIsCleared()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node1 = TestFixtures.CreateTriggerNode("node1");
        var node2 = TestFixtures.CreateHttpRequestNode("node2");
        
        service.AddNode(node1);
        service.AddNode(node2);
        service.Undo();
        Assert.True(service.CanRedo);

        // Act - remove a node (new action)
        service.RemoveNode("node1");

        // Assert - redo should no longer be available
        Assert.False(service.CanRedo);
    }

    /// <summary>
    /// Tests that redo stack is cleared when updating workflow metadata after undo.
    /// Validates: Requirements 3.4
    /// </summary>
    [Fact]
    public void WhenUpdateMetadataAfterUndoThenRedoStackIsCleared()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node = TestFixtures.CreateTriggerNode();
        service.AddNode(node);
        service.Undo();
        Assert.True(service.CanRedo);

        // Act - update metadata (new action)
        service.UpdateWorkflowMetadata("New Name", "New Description");

        // Assert - redo should no longer be available
        Assert.False(service.CanRedo);
    }

    #endregion

    #region Undo/Redo Interaction Tests

    /// <summary>
    /// Tests that undo followed by redo returns to original state.
    /// Validates: Requirements 3.2, 3.3
    /// </summary>
    [Fact]
    public void WhenUndoThenRedoThenOriginalStateIsRestored()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node = TestFixtures.CreateTriggerNode();
        service.AddNode(node);
        var originalNodeCount = service.Workflow.Nodes.Count;
        var originalNodeId = service.Workflow.Nodes[0].Id;

        // Act
        service.Undo();
        service.Redo();

        // Assert
        Assert.Equal(originalNodeCount, service.Workflow.Nodes.Count);
        Assert.Equal(originalNodeId, service.Workflow.Nodes[0].Id);
    }


    /// <summary>
    /// Tests complex undo/redo sequence.
    /// Validates: Requirements 3.1, 3.2, 3.3, 3.4
    /// </summary>
    [Fact]
    public void WhenComplexUndoRedoSequenceThenStatesAreCorrect()
    {
        // Arrange
        var service = new WorkflowStateService();
        var node1 = TestFixtures.CreateTriggerNode("node1");
        var node2 = TestFixtures.CreateHttpRequestNode("node2");
        var node3 = TestFixtures.CreateEmailSendNode("node3");

        // Add three nodes
        service.AddNode(node1);
        service.AddNode(node2);
        service.AddNode(node3);
        Assert.Equal(3, service.Workflow.Nodes.Count);

        // Undo twice
        service.Undo();
        service.Undo();
        Assert.Single(service.Workflow.Nodes);
        Assert.True(service.CanRedo);

        // Redo once
        service.Redo();
        Assert.Equal(2, service.Workflow.Nodes.Count);

        // Add a new node (should clear redo stack)
        var node4 = TestFixtures.CreateDatabaseQueryNode("node4");
        service.AddNode(node4);
        Assert.Equal(3, service.Workflow.Nodes.Count);
        Assert.False(service.CanRedo);

        // Verify node3 is not in the workflow (was undone and redo was cleared)
        Assert.DoesNotContain(service.Workflow.Nodes, n => n.Id == "node3");
        Assert.Contains(service.Workflow.Nodes, n => n.Id == "node4");
    }

    #endregion

    #region Property Tests

    /// <summary>
    /// Feature: blazor-integration-tests, Property 8: Undo Restores Previous State
    /// For any workflow modification action, performing the action then undoing 
    /// SHALL restore the workflow to its state before the action.
    /// Validates: Requirements 3.1, 3.2
    /// </summary>
    [Fact]
    public void Property8_UndoRestoresPreviousState()
    {
        DesignerGenerators.NodeGen.Sample(node =>
        {
            // Arrange
            var service = new WorkflowStateService();
            
            // Capture state before action
            var nodeCountBefore = service.Workflow.Nodes.Count;

            // Act - perform action
            service.AddNode(node);
            Assert.Equal(nodeCountBefore + 1, service.Workflow.Nodes.Count);
            Assert.True(service.CanUndo);

            // Act - undo
            service.Undo();

            // Assert - state is restored
            Assert.Equal(nodeCountBefore, service.Workflow.Nodes.Count);
            Assert.DoesNotContain(service.Workflow.Nodes, n => n.Id == node.Id);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: blazor-integration-tests, Property 8 (Extended): Undo Restores Previous State for Node Removal
    /// For any workflow with nodes, removing a node then undoing 
    /// SHALL restore the node to the workflow.
    /// Validates: Requirements 3.1, 3.2
    /// </summary>
    [Fact]
    public void Property8_UndoRestoresPreviousState_NodeRemoval()
    {
        DesignerGenerators.ValidWorkflowGen.Sample(workflow =>
        {
            // Skip if workflow has no nodes
            if (workflow.Nodes.Count == 0)
                return;

            // Arrange
            var service = new WorkflowStateService();
            service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
            service.LoadWorkflow(workflow);
            
            var nodeToRemove = workflow.Nodes[0];
            var nodeCountBefore = service.Workflow.Nodes.Count;

            // Act - remove node
            service.RemoveNode(nodeToRemove.Id);
            Assert.Equal(nodeCountBefore - 1, service.Workflow.Nodes.Count);
            Assert.True(service.CanUndo);

            // Act - undo
            service.Undo();

            // Assert - node is restored
            Assert.Equal(nodeCountBefore, service.Workflow.Nodes.Count);
            Assert.Contains(service.Workflow.Nodes, n => n.Id == nodeToRemove.Id);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: blazor-integration-tests, Property 9: Redo Restores Undone State
    /// For any workflow modification action, performing the action, undoing, then redoing 
    /// SHALL restore the workflow to its state after the action.
    /// Validates: Requirements 3.3
    /// </summary>
    [Fact]
    public void Property9_RedoRestoresUndoneState()
    {
        DesignerGenerators.NodeGen.Sample(node =>
        {
            // Arrange
            var service = new WorkflowStateService();
            
            // Perform action
            service.AddNode(node);
            var nodeCountAfterAction = service.Workflow.Nodes.Count;
            var nodeExistsAfterAction = service.Workflow.Nodes.Any(n => n.Id == node.Id);

            // Undo
            service.Undo();
            Assert.True(service.CanRedo);

            // Act - redo
            service.Redo();

            // Assert - state after action is restored
            Assert.Equal(nodeCountAfterAction, service.Workflow.Nodes.Count);
            Assert.Equal(nodeExistsAfterAction, service.Workflow.Nodes.Any(n => n.Id == node.Id));
        }, iter: 100);
    }

    /// <summary>
    /// Feature: blazor-integration-tests, Property 9 (Extended): Redo Restores Undone State for Multiple Operations
    /// For any sequence of workflow modifications, undoing all then redoing all
    /// SHALL restore the workflow to its final state.
    /// Validates: Requirements 3.3
    /// </summary>
    [Fact]
    public void Property9_RedoRestoresUndoneState_MultipleOperations()
    {
        var testGen = from nodes in DesignerGenerators.NodeGen.List[1, 5]
                      select nodes;

        testGen.Sample(nodes =>
        {
            // Arrange
            var service = new WorkflowStateService();
            
            // Add all nodes
            foreach (var node in nodes)
            {
                service.AddNode(node);
            }
            
            var finalNodeCount = service.Workflow.Nodes.Count;
            var finalNodeIds = service.Workflow.Nodes.Select(n => n.Id).ToHashSet();

            // Undo all
            for (var i = 0; i < nodes.Count; i++)
            {
                service.Undo();
            }
            Assert.Empty(service.Workflow.Nodes);

            // Act - redo all
            for (var i = 0; i < nodes.Count; i++)
            {
                service.Redo();
            }

            // Assert - final state is restored
            Assert.Equal(finalNodeCount, service.Workflow.Nodes.Count);
            foreach (var nodeId in finalNodeIds)
            {
                Assert.Contains(service.Workflow.Nodes, n => n.Id == nodeId);
            }
        }, iter: 100);
    }

    /// <summary>
    /// Property test: New action after undo clears redo stack.
    /// For any workflow modification followed by undo, performing a new action
    /// SHALL clear the redo stack.
    /// Validates: Requirements 3.4
    /// </summary>
    [Fact]
    public void Property_NewActionAfterUndoClearsRedoStack()
    {
        var testGen = from node1 in DesignerGenerators.NodeGen
                      from node2 in DesignerGenerators.NodeGen
                      select (node1, node2);

        testGen.Sample(data =>
        {
            var (node1, node2) = data;
            
            // Arrange
            var service = new WorkflowStateService();
            service.AddNode(node1);
            service.Undo();
            Assert.True(service.CanRedo);

            // Act - perform new action
            service.AddNode(node2);

            // Assert - redo stack is cleared
            Assert.False(service.CanRedo);
        }, iter: 100);
    }

    #endregion
}
