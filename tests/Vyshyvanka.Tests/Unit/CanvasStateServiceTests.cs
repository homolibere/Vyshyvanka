using Vyshyvanka.Core.Models;
using Vyshyvanka.Designer.Services;

namespace Vyshyvanka.Tests.Unit;

public class CanvasStateServiceTests
{
    private readonly WorkflowStore _store = new();
    private readonly CanvasStateService _sut;

    public CanvasStateServiceTests()
    {
        _sut = new CanvasStateService(_store);
    }

    [Fact]
    public void WhenCreatedThenCanvasStateIsDefault()
    {
        _sut.CanvasState.Zoom.Should().Be(1.0);
        _sut.CanvasState.PanX.Should().Be(0);
        _sut.CanvasState.PanY.Should().Be(0);
    }

    [Fact]
    public void WhenCreatedThenNothingIsSelected()
    {
        _sut.SelectedNodeId.Should().BeNull();
        _sut.SelectedConnection.Should().BeNull();
        _sut.PendingConnection.Should().BeNull();
    }

    [Fact]
    public void WhenSelectNodeThenSelectedNodeIdIsSet()
    {
        _sut.SelectNode("node-1");

        _sut.SelectedNodeId.Should().Be("node-1");
    }

    [Fact]
    public void WhenSelectNodeThenConnectionIsCleared()
    {
        _sut.SelectConnection(new Connection { SourceNodeId = "a", TargetNodeId = "b" });

        _sut.SelectNode("node-1");

        _sut.SelectedConnection.Should().BeNull();
    }

    [Fact]
    public void WhenSelectConnectionThenNodeIsCleared()
    {
        _sut.SelectNode("node-1");

        _sut.SelectConnection(new Connection { SourceNodeId = "a", TargetNodeId = "b" });

        _sut.SelectedNodeId.Should().BeNull();
        _sut.SelectedConnection.Should().NotBeNull();
    }

    [Fact]
    public void WhenClearSelectionThenBothAreNull()
    {
        _sut.SelectNode("node-1");

        _sut.ClearSelection();

        _sut.SelectedNodeId.Should().BeNull();
        _sut.SelectedConnection.Should().BeNull();
    }

    [Fact]
    public void WhenPanThenOffsetIsUpdated()
    {
        _sut.Pan(10, 20);

        _sut.CanvasState.PanX.Should().Be(10);
        _sut.CanvasState.PanY.Should().Be(20);
    }

    [Fact]
    public void WhenPanMultipleTimesThenOffsetsAccumulate()
    {
        _sut.Pan(10, 20);
        _sut.Pan(5, -10);

        _sut.CanvasState.PanX.Should().Be(15);
        _sut.CanvasState.PanY.Should().Be(10);
    }

    [Fact]
    public void WhenZoomThenZoomLevelIsUpdated()
    {
        _sut.Zoom(1.5);

        _sut.CanvasState.Zoom.Should().Be(1.5);
    }

    [Fact]
    public void WhenZoomBeyondMaxThenClampedTo2()
    {
        _sut.Zoom(5.0);

        _sut.CanvasState.Zoom.Should().Be(2.0);
    }

    [Fact]
    public void WhenZoomBelowMinThenClampedTo025()
    {
        _sut.Zoom(0.1);

        _sut.CanvasState.Zoom.Should().Be(0.25);
    }

    [Fact]
    public void WhenSetCanvasSizeThenDimensionsAreUpdated()
    {
        _sut.SetCanvasSize(1920, 1080);

        _sut.CanvasState.Width.Should().Be(1920);
        _sut.CanvasState.Height.Should().Be(1080);
    }

    [Fact]
    public void WhenStartConnectionThenPendingConnectionIsSet()
    {
        _sut.StartConnection("node-1", "output", 100, 200);

        _sut.PendingConnection.Should().NotBeNull();
        _sut.PendingConnection!.SourceNodeId.Should().Be("node-1");
        _sut.PendingConnection.SourcePort.Should().Be("output");
        _sut.PendingConnection.CurrentX.Should().Be(100);
        _sut.PendingConnection.CurrentY.Should().Be(200);
    }

    [Fact]
    public void WhenUpdatePendingConnectionThenPositionIsUpdated()
    {
        _sut.StartConnection("node-1", "output", 100, 200);

        _sut.UpdatePendingConnection(150, 250);

        _sut.PendingConnection!.CurrentX.Should().Be(150);
        _sut.PendingConnection.CurrentY.Should().Be(250);
    }

    [Fact]
    public void WhenUpdatePendingConnectionWithNoPendingThenNoOp()
    {
        _sut.UpdatePendingConnection(150, 250);

        _sut.PendingConnection.Should().BeNull();
    }

    [Fact]
    public void WhenCancelPendingConnectionThenPendingIsNull()
    {
        _sut.StartConnection("node-1", "output", 100, 200);

        _sut.CancelPendingConnection();

        _sut.PendingConnection.Should().BeNull();
    }

    [Fact]
    public void WhenSaveUndoStateThenCanUndoIsTrue()
    {
        _sut.SaveUndoState("Test action");

        _sut.CanUndo.Should().BeTrue();
    }

    [Fact]
    public void WhenUndoThenWorkflowIsRestored()
    {
        var originalName = _store.Workflow.Name;
        _sut.SaveUndoState("Change name");
        _store.SetWorkflow(_store.Workflow with { Name = "Changed" });

        _sut.Undo();

        _store.Workflow.Name.Should().Be(originalName);
    }

    [Fact]
    public void WhenUndoThenCanRedoIsTrue()
    {
        _sut.SaveUndoState("Action");
        _store.SetWorkflow(_store.Workflow with { Name = "Changed" });

        _sut.Undo();

        _sut.CanRedo.Should().BeTrue();
    }

    [Fact]
    public void WhenRedoThenWorkflowIsReapplied()
    {
        _sut.SaveUndoState("Action");
        _store.SetWorkflow(_store.Workflow with { Name = "Changed" });
        var changedWorkflow = _store.Workflow;

        _sut.Undo();
        _sut.Redo();

        _store.Workflow.Name.Should().Be("Changed");
    }

    [Fact]
    public void WhenUndoWithEmptyStackThenNoOp()
    {
        var originalName = _store.Workflow.Name;

        _sut.Undo();

        _store.Workflow.Name.Should().Be(originalName);
    }

    [Fact]
    public void WhenRedoWithEmptyStackThenNoOp()
    {
        _sut.Redo();

        _sut.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void WhenSaveUndoStateThenRedoStackIsCleared()
    {
        _sut.SaveUndoState("First");
        _store.SetWorkflow(_store.Workflow with { Name = "Changed" });
        _sut.Undo();
        _sut.CanRedo.Should().BeTrue();

        _sut.SaveUndoState("Second");

        _sut.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void WhenStartDragFromPaletteThenDraggedNodeTypeIsSet()
    {
        _sut.StartDragFromPalette("http-request");

        _sut.DraggedNodeType.Should().Be("http-request");
    }

    [Fact]
    public void WhenEndDragFromPaletteThenDraggedNodeTypeIsNull()
    {
        _sut.StartDragFromPalette("http-request");

        _sut.EndDragFromPalette();

        _sut.DraggedNodeType.Should().BeNull();
    }

    [Fact]
    public void WhenStateChangesThenNotifiesViaStore()
    {
        var notified = false;
        _store.OnStateChanged += () => notified = true;

        _sut.SelectNode("node-1");

        notified.Should().BeTrue();
    }
}

public class CanvasStateServiceUndoCapTests
{
    private readonly WorkflowStore _store = new();
    private readonly CanvasStateService _sut;

    public CanvasStateServiceUndoCapTests()
    {
        _sut = new CanvasStateService(_store);
    }

    [Fact]
    public void WhenMoreThanMaxUndoHistoryPushedThenStackIsCapped()
    {
        for (var i = 0; i < CanvasStateService.MaxUndoHistory + 20; i++)
        {
            _sut.SaveUndoState($"Action {i}");
        }

        // Undo should only succeed MaxUndoHistory times
        var undoCount = 0;
        while (_sut.CanUndo)
        {
            _sut.Undo();
            undoCount++;
        }

        undoCount.Should().Be(CanvasStateService.MaxUndoHistory);
    }

    [Fact]
    public void WhenUndoStackExceedsCapThenOldestEntryIsDiscarded()
    {
        // Push one entry and remember the workflow state
        _store.SetWorkflow(_store.Workflow with { Name = "First" });
        _sut.SaveUndoState("First action");

        // Push MaxUndoHistory more entries to evict the first one
        for (var i = 0; i < CanvasStateService.MaxUndoHistory; i++)
        {
            _store.SetWorkflow(_store.Workflow with { Name = $"State {i}" });
            _sut.SaveUndoState($"Action {i}");
        }

        // Undo all the way back — should never restore "First"
        while (_sut.CanUndo)
        {
            _sut.Undo();
        }

        _store.Workflow.Name.Should().NotBe("First");
    }
}
