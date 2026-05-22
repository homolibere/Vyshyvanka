using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Designer.Services;

namespace Vyshyvanka.Tests.Unit;

public class WorkflowEditServiceTests
{
    private readonly WorkflowStore _store = new();
    private readonly CanvasStateService _canvasState;
    private readonly WorkflowValidationService _validationService;
    private readonly ExecutionStateService _executionState;
    private readonly WorkflowEditService _sut;

    public WorkflowEditServiceTests()
    {
        _canvasState = new CanvasStateService(_store);
        _validationService = new WorkflowValidationService(_store);
        _executionState = new ExecutionStateService(_store);
        _sut = new WorkflowEditService(_store, _canvasState, _validationService, _executionState);
    }

    [Fact]
    public void WhenAddNodeThenNodeIsInWorkflow()
    {
        var node = new WorkflowNode { Id = "n1", Type = "if", Name = "If" };

        _sut.AddNode(node);

        _store.Workflow.Nodes.Should().HaveCount(1);
        _store.Workflow.Nodes[0].Id.Should().Be("n1");
    }

    [Fact]
    public void WhenAddNodeThenNodeIsSelected()
    {
        var node = new WorkflowNode { Id = "n1", Type = "if", Name = "If" };

        _sut.AddNode(node);

        _canvasState.SelectedNodeId.Should().Be("n1");
    }

    [Fact]
    public void WhenAddNodeThenWorkflowIsDirty()
    {
        _sut.AddNode(new WorkflowNode { Id = "n1", Type = "if", Name = "If" });

        _store.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void WhenAddNodeThenUndoIsAvailable()
    {
        _sut.AddNode(new WorkflowNode { Id = "n1", Type = "if", Name = "If" });

        _canvasState.CanUndo.Should().BeTrue();
    }

    [Fact]
    public void WhenRemoveNodeThenNodeIsGone()
    {
        _sut.AddNode(new WorkflowNode { Id = "n1", Type = "if", Name = "If" });

        _sut.RemoveNode("n1");

        _store.Workflow.Nodes.Should().BeEmpty();
    }

    [Fact]
    public void WhenRemoveNodeThenRelatedConnectionsAreRemoved()
    {
        _store.SetWorkflow(_store.Workflow with
        {
            Nodes =
            [
                new WorkflowNode { Id = "n1", Type = "trigger", Name = "Trigger" },
                new WorkflowNode { Id = "n2", Type = "action", Name = "Action" },
                new WorkflowNode { Id = "n3", Type = "action", Name = "Other" }
            ],
            Connections =
            [
                new Connection { SourceNodeId = "n1", SourcePort = "out", TargetNodeId = "n2", TargetPort = "in" },
                new Connection { SourceNodeId = "n2", SourcePort = "out", TargetNodeId = "n3", TargetPort = "in" }
            ]
        });

        _sut.RemoveNode("n2");

        _store.Workflow.Connections.Should().BeEmpty();
    }

    [Fact]
    public void WhenRemoveSelectedNodeThenSelectionIsCleared()
    {
        _sut.AddNode(new WorkflowNode { Id = "n1", Type = "if", Name = "If" });
        _canvasState.SelectedNodeId.Should().Be("n1");

        _sut.RemoveNode("n1");

        _canvasState.SelectedNodeId.Should().BeNull();
    }

    [Fact]
    public void WhenMoveNodeThenPositionIsUpdated()
    {
        _sut.AddNode(new WorkflowNode { Id = "n1", Type = "if", Name = "If", Position = new Position(0, 0) });

        _sut.MoveNode("n1", 100, 200);

        _store.GetNode("n1")!.Position.X.Should().Be(100);
        _store.GetNode("n1")!.Position.Y.Should().Be(200);
    }

    [Fact]
    public void WhenMoveNonexistentNodeThenNoOp()
    {
        _sut.MoveNode("nonexistent", 100, 200);

        _store.Workflow.Nodes.Should().BeEmpty();
    }

    [Fact]
    public void WhenAddConnectionThenConnectionIsInWorkflow()
    {
        _store.SetWorkflow(_store.Workflow with
        {
            Nodes =
            [
                new WorkflowNode { Id = "n1", Type = "trigger", Name = "Trigger" },
                new WorkflowNode { Id = "n2", Type = "action", Name = "Action" }
            ]
        });
        var connection = new Connection { SourceNodeId = "n1", SourcePort = "out", TargetNodeId = "n2", TargetPort = "in" };

        _sut.AddConnection(connection);

        _store.Workflow.Connections.Should().HaveCount(1);
    }

    [Fact]
    public void WhenAddDuplicateConnectionThenIgnored()
    {
        var connection = new Connection { SourceNodeId = "n1", SourcePort = "out", TargetNodeId = "n2", TargetPort = "in" };
        _store.SetWorkflow(_store.Workflow with
        {
            Nodes =
            [
                new WorkflowNode { Id = "n1", Type = "trigger", Name = "Trigger" },
                new WorkflowNode { Id = "n2", Type = "action", Name = "Action" }
            ],
            Connections = [connection]
        });

        _sut.AddConnection(connection);

        _store.Workflow.Connections.Should().HaveCount(1);
    }

    [Fact]
    public void WhenRemoveConnectionThenConnectionIsGone()
    {
        var connection = new Connection { SourceNodeId = "n1", SourcePort = "out", TargetNodeId = "n2", TargetPort = "in" };
        _store.SetWorkflow(_store.Workflow with
        {
            Nodes =
            [
                new WorkflowNode { Id = "n1", Type = "trigger", Name = "Trigger" },
                new WorkflowNode { Id = "n2", Type = "action", Name = "Action" }
            ],
            Connections = [connection]
        });

        _sut.RemoveConnection(connection);

        _store.Workflow.Connections.Should().BeEmpty();
    }

    [Fact]
    public void WhenUpdateWorkflowMetadataThenNameIsUpdated()
    {
        _sut.UpdateWorkflowMetadata("New Name", "New Description");

        _store.Workflow.Name.Should().Be("New Name");
        _store.Workflow.Description.Should().Be("New Description");
    }

    [Fact]
    public void WhenToggleWorkflowActiveThenStateFlips()
    {
        var original = _store.Workflow.IsActive;

        _sut.ToggleWorkflowActive();

        _store.Workflow.IsActive.Should().Be(!original);
    }

    [Fact]
    public void WhenSetWorkflowActiveToSameValueThenNoOp()
    {
        var original = _store.Workflow.IsActive;

        _sut.SetWorkflowActive(original);

        _canvasState.CanUndo.Should().BeFalse(); // No undo state saved
    }

    [Fact]
    public void WhenUpdateNodeNameThenNameIsChanged()
    {
        _sut.AddNode(new WorkflowNode { Id = "n1", Type = "if", Name = "Old Name" });

        _sut.UpdateNodeName("n1", "New Name");

        _store.GetNode("n1")!.Name.Should().Be("New Name");
    }

    [Fact]
    public void WhenUpdateNodeNameWithEmptyStringThenNoOp()
    {
        _sut.AddNode(new WorkflowNode { Id = "n1", Type = "if", Name = "Original" });

        _sut.UpdateNodeName("n1", "");

        _store.GetNode("n1")!.Name.Should().Be("Original");
    }

    [Fact]
    public void WhenNewWorkflowThenWorkflowIsReset()
    {
        _sut.AddNode(new WorkflowNode { Id = "n1", Type = "if", Name = "If" });

        _sut.NewWorkflow();

        _store.Workflow.Nodes.Should().BeEmpty();
        _store.Workflow.Name.Should().Be("New Workflow");
        _store.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void WhenLoadWorkflowThenWorkflowIsReplaced()
    {
        var loaded = new Workflow
        {
            Id = Guid.NewGuid(),
            Name = "Loaded Workflow",
            Version = 3,
            IsActive = true,
            Nodes = [new WorkflowNode { Id = "x1", Type = "if", Name = "Loaded Node" }]
        };

        _sut.LoadWorkflow(loaded);

        _store.Workflow.Name.Should().Be("Loaded Workflow");
        _store.Workflow.Nodes.Should().HaveCount(1);
        _store.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void WhenDropNodeFromPaletteThenNodeIsCreated()
    {
        _store.SetNodeDefinitions([new NodeDefinition { Type = "http-request", Name = "HTTP Request", Category = NodeCategory.Action }]);
        _canvasState.StartDragFromPalette("http-request");

        _sut.DropNodeFromPalette(100, 200);

        _store.Workflow.Nodes.Should().HaveCount(1);
        _store.Workflow.Nodes[0].Type.Should().Be("http-request");
        _store.Workflow.Nodes[0].Position.X.Should().Be(100);
        _store.Workflow.Nodes[0].Position.Y.Should().Be(200);
    }

    [Fact]
    public void WhenDropNodeFromPaletteWithNoDragThenNoOp()
    {
        _sut.DropNodeFromPalette(100, 200);

        _store.Workflow.Nodes.Should().BeEmpty();
    }

    [Fact]
    public void WhenDropNodeFromPaletteWithUnknownTypeThenNoOp()
    {
        _canvasState.StartDragFromPalette("nonexistent");

        _sut.DropNodeFromPalette(100, 200);

        _store.Workflow.Nodes.Should().BeEmpty();
        _canvasState.DraggedNodeType.Should().BeNull();
    }
}
