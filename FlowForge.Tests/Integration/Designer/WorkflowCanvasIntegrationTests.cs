using Bunit;
using CsCheck;
using FlowForge.Designer.Components;
using FlowForge.Designer.Services;
using FlowForge.Tests.Integration.Designer.Generators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using static FlowForge.Designer.Components.WorkflowCanvas;

namespace FlowForge.Tests.Integration.Designer;

/// <summary>
/// Integration tests for WorkflowCanvas component.
/// Tests node rendering from workflow state and zoom bounds enforcement.
/// </summary>
public class WorkflowCanvasIntegrationTests : TestContext
{
    public WorkflowCanvasIntegrationTests()
    {
        // Setup JSInterop for canvasInterop calls
        JSInterop.Setup<CanvasDimensions>("canvasInterop.getElementDimensions", _ => true)
            .SetResult(new CanvasDimensions(800, 600));
        // Use SetupVoid for observeResize since it returns IJSObjectReference
        JSInterop.SetupVoid("canvasInterop.observeResize", _ => true);
        JSInterop.SetupVoid("canvasInterop.disconnectObserver", _ => true);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    #region Node Rendering Tests (Task 11.1)

    /// <summary>
    /// Tests that when a workflow is loaded, all nodes are rendered on the canvas.
    /// Validates: Requirements 7.1
    /// </summary>
    [Fact]
    public void WhenWorkflowLoadedThenAllNodesAreRendered()
    {
        // Arrange
        var stateService = new WorkflowStateService();
        var workflow = TestFixtures.CreateSimpleWorkflow();
        var definitions = TestFixtures.CreateCommonNodeDefinitions();
        
        stateService.SetNodeDefinitions(definitions);
        stateService.LoadWorkflow(workflow);
        
        Services.AddSingleton(stateService);

        // Act
        var cut = Render<WorkflowCanvas>();

        // Assert - Each node should have a corresponding canvas-node element
        var nodeElements = cut.FindAll(".canvas-node");
        Assert.Equal(workflow.Nodes.Count, nodeElements.Count);
    }

    /// <summary>
    /// Tests that nodes are rendered at their correct positions.
    /// Validates: Requirements 7.1
    /// </summary>
    [Fact]
    public void WhenWorkflowLoadedThenNodesAreRenderedAtCorrectPositions()
    {
        // Arrange
        var stateService = new WorkflowStateService();
        var workflow = TestFixtures.CreateSimpleWorkflow();
        var definitions = TestFixtures.CreateCommonNodeDefinitions();
        
        stateService.SetNodeDefinitions(definitions);
        stateService.LoadWorkflow(workflow);
        
        Services.AddSingleton(stateService);

        // Act
        var cut = Render<WorkflowCanvas>();

        // Assert - Each node should have a transform with its position
        foreach (var node in workflow.Nodes)
        {
            var expectedTransform = $"translate({node.Position.X}, {node.Position.Y})";
            Assert.Contains(expectedTransform, cut.Markup);
        }
    }

    /// <summary>
    /// Tests that an empty workflow renders no nodes.
    /// Validates: Requirements 7.1
    /// </summary>
    [Fact]
    public void WhenEmptyWorkflowLoadedThenNoNodesAreRendered()
    {
        // Arrange
        var stateService = new WorkflowStateService();
        var workflow = TestFixtures.CreateEmptyWorkflow();
        var definitions = TestFixtures.CreateCommonNodeDefinitions();
        
        stateService.SetNodeDefinitions(definitions);
        stateService.LoadWorkflow(workflow);
        
        Services.AddSingleton(stateService);

        // Act
        var cut = Render<WorkflowCanvas>();

        // Assert - No canvas-node elements should be present
        var nodeElements = cut.FindAll(".canvas-node");
        Assert.Empty(nodeElements);
    }

    /// <summary>
    /// Tests that adding a node updates the canvas rendering.
    /// Validates: Requirements 7.1
    /// </summary>
    [Fact]
    public void WhenNodeAddedThenCanvasUpdates()
    {
        // Arrange
        var stateService = new WorkflowStateService();
        var definitions = TestFixtures.CreateCommonNodeDefinitions();
        
        stateService.SetNodeDefinitions(definitions);
        stateService.NewWorkflow();
        
        Services.AddSingleton(stateService);
        var cut = Render<WorkflowCanvas>();

        // Initial state - no nodes
        var initialNodeCount = cut.FindAll(".canvas-node").Count;
        Assert.Equal(0, initialNodeCount);

        // Act - Add a node (use InvokeAsync to handle dispatcher context)
        var newNode = TestFixtures.CreateTriggerNode();
        cut.InvokeAsync(() => stateService.AddNode(newNode));

        // Assert - Node should now be rendered
        var nodeElements = cut.FindAll(".canvas-node");
        Assert.Single(nodeElements);
    }

    /// <summary>
    /// Tests that removing a node updates the canvas rendering.
    /// Validates: Requirements 7.1
    /// </summary>
    [Fact]
    public void WhenNodeRemovedThenCanvasUpdates()
    {
        // Arrange
        var stateService = new WorkflowStateService();
        var workflow = TestFixtures.CreateSimpleWorkflow();
        var definitions = TestFixtures.CreateCommonNodeDefinitions();
        
        stateService.SetNodeDefinitions(definitions);
        stateService.LoadWorkflow(workflow);
        
        Services.AddSingleton(stateService);
        var cut = Render<WorkflowCanvas>();

        var initialNodeCount = cut.FindAll(".canvas-node").Count;
        Assert.Equal(2, initialNodeCount);

        // Act - Remove a node (use InvokeAsync to handle dispatcher context)
        var nodeToRemove = workflow.Nodes[0];
        cut.InvokeAsync(() => stateService.RemoveNode(nodeToRemove.Id));

        // Assert - One less node should be rendered
        var nodeElements = cut.FindAll(".canvas-node");
        Assert.Single(nodeElements);
    }

    #endregion

    #region Zoom Bounds Tests (Task 11.1)

    /// <summary>
    /// Tests that zoom is clamped to minimum bound (0.25).
    /// Validates: Requirements 7.4
    /// </summary>
    [Fact]
    public void WhenZoomBelowMinimumThenClampedToMinimum()
    {
        // Arrange
        var stateService = new WorkflowStateService();

        // Act - Try to zoom below minimum
        stateService.Zoom(0.1);

        // Assert - Zoom should be clamped to 0.25
        Assert.Equal(0.25, stateService.CanvasState.Zoom);
    }

    /// <summary>
    /// Tests that zoom is clamped to maximum bound (2.0).
    /// Validates: Requirements 7.4
    /// </summary>
    [Fact]
    public void WhenZoomAboveMaximumThenClampedToMaximum()
    {
        // Arrange
        var stateService = new WorkflowStateService();

        // Act - Try to zoom above maximum
        stateService.Zoom(3.0);

        // Assert - Zoom should be clamped to 2.0
        Assert.Equal(2.0, stateService.CanvasState.Zoom);
    }

    /// <summary>
    /// Tests that zoom within bounds is applied correctly.
    /// Validates: Requirements 7.4
    /// </summary>
    [Fact]
    public void WhenZoomWithinBoundsThenAppliedCorrectly()
    {
        // Arrange
        var stateService = new WorkflowStateService();

        // Act - Zoom to a valid value
        stateService.Zoom(1.5);

        // Assert - Zoom should be exactly 1.5
        Assert.Equal(1.5, stateService.CanvasState.Zoom);
    }

    /// <summary>
    /// Tests that zoom at exact minimum bound is valid.
    /// Validates: Requirements 7.4
    /// </summary>
    [Fact]
    public void WhenZoomAtMinimumBoundThenValid()
    {
        // Arrange
        var stateService = new WorkflowStateService();

        // Act
        stateService.Zoom(0.25);

        // Assert
        Assert.Equal(0.25, stateService.CanvasState.Zoom);
    }

    /// <summary>
    /// Tests that zoom at exact maximum bound is valid.
    /// Validates: Requirements 7.4
    /// </summary>
    [Fact]
    public void WhenZoomAtMaximumBoundThenValid()
    {
        // Arrange
        var stateService = new WorkflowStateService();

        // Act
        stateService.Zoom(2.0);

        // Assert
        Assert.Equal(2.0, stateService.CanvasState.Zoom);
    }

    /// <summary>
    /// Tests that negative zoom values are clamped to minimum.
    /// Validates: Requirements 7.4
    /// </summary>
    [Fact]
    public void WhenZoomNegativeThenClampedToMinimum()
    {
        // Arrange
        var stateService = new WorkflowStateService();

        // Act - Try negative zoom
        stateService.Zoom(-1.0);

        // Assert - Should be clamped to minimum
        Assert.Equal(0.25, stateService.CanvasState.Zoom);
    }

    #endregion

    #region Property Tests

    /// <summary>
    /// Feature: blazor-integration-tests, Property 14: Canvas Node Rendering
    /// For any workflow with nodes, the WorkflowCanvas SHALL render a visual element for each node in the workflow.
    /// Validates: Requirements 7.1
    /// </summary>
    [Fact]
    public void Property14_CanvasNodeRendering()
    {
        // Generate workflows with varying numbers of nodes
        var workflowGen = DesignerGenerators.WorkflowGen;

        workflowGen.Sample(workflow =>
        {
            // Create a fresh TestContext for each iteration
            using var ctx = new TestContext();
            
            // Setup JSInterop for canvasInterop calls
            ctx.JSInterop.Setup<CanvasDimensions>("canvasInterop.getElementDimensions", _ => true)
                .SetResult(new CanvasDimensions(800, 600));
            ctx.JSInterop.SetupVoid("canvasInterop.observeResize", _ => true);
            ctx.JSInterop.SetupVoid("canvasInterop.disconnectObserver", _ => true);
            ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            
            // Arrange
            var stateService = new WorkflowStateService();
            var definitions = TestFixtures.CreateCommonNodeDefinitions();
            
            stateService.SetNodeDefinitions(definitions);
            stateService.LoadWorkflow(workflow);
            
            ctx.Services.AddSingleton(stateService);

            // Act
            var cut = ctx.Render<WorkflowCanvas>();

            // Assert - The number of canvas-node elements should equal the number of nodes
            var nodeElements = cut.FindAll(".canvas-node");
            Assert.Equal(workflow.Nodes.Count, nodeElements.Count);

            // Assert - Each node's position should be in the markup as a transform
            foreach (var node in workflow.Nodes)
            {
                var expectedTransform = $"translate({node.Position.X}, {node.Position.Y})";
                Assert.Contains(expectedTransform, cut.Markup);
            }
        }, iter: 100);
    }

    /// <summary>
    /// Feature: blazor-integration-tests, Property 15: Canvas Zoom Bounds
    /// For any zoom operation, the canvas zoom level SHALL remain within the bounds [0.25, 2.0].
    /// Validates: Requirements 7.4
    /// </summary>
    [Fact]
    public void Property15_CanvasZoomBounds()
    {
        // Generate random zoom values across a wide range
        var zoomGen = Gen.Double[-10.0, 10.0];

        zoomGen.Sample(zoomValue =>
        {
            // Arrange
            var stateService = new WorkflowStateService();

            // Act
            stateService.Zoom(zoomValue);

            // Assert - Zoom should always be within bounds [0.25, 2.0]
            Assert.InRange(stateService.CanvasState.Zoom, 0.25, 2.0);

            // Additional assertion: if input was within bounds, output should match
            if (zoomValue >= 0.25 && zoomValue <= 2.0)
            {
                Assert.Equal(zoomValue, stateService.CanvasState.Zoom);
            }
            // If input was below minimum, output should be minimum
            else if (zoomValue < 0.25)
            {
                Assert.Equal(0.25, stateService.CanvasState.Zoom);
            }
            // If input was above maximum, output should be maximum
            else
            {
                Assert.Equal(2.0, stateService.CanvasState.Zoom);
            }
        }, iter: 100);
    }

    #endregion
}
