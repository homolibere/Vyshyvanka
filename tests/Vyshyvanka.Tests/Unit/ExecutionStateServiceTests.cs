using Vyshyvanka.Contracts.Executions;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;

namespace Vyshyvanka.Tests.Unit;

public class ExecutionStateServiceTests
{
    private readonly WorkflowStore _store = new();
    private readonly ExecutionStateService _sut;

    public ExecutionStateServiceTests()
    {
        _sut = new ExecutionStateService(_store);
    }

    [Fact]
    public void WhenCreatedThenNoExecutionIsActive()
    {
        _sut.CurrentExecution.Should().BeNull();
        _sut.IsExecutionActive.Should().BeFalse();
    }

    [Fact]
    public void WhenSetCurrentExecutionThenExecutionIsStored()
    {
        var execution = CreateExecution(ExecutionStatus.Running);

        _sut.SetCurrentExecution(execution);

        _sut.CurrentExecution.Should().Be(execution);
        _sut.IsExecutionActive.Should().BeTrue();
    }

    [Fact]
    public void WhenSetCurrentExecutionToNullThenCleared()
    {
        _sut.SetCurrentExecution(CreateExecution(ExecutionStatus.Running));

        _sut.SetCurrentExecution(null);

        _sut.CurrentExecution.Should().BeNull();
        _sut.IsExecutionActive.Should().BeFalse();
    }

    [Fact]
    public void WhenExecutionIsCompletedThenIsExecutionActiveIsFalse()
    {
        _sut.SetCurrentExecution(CreateExecution(ExecutionStatus.Completed));

        _sut.IsExecutionActive.Should().BeFalse();
    }

    [Fact]
    public void WhenExecutionIsFailedThenIsExecutionActiveIsFalse()
    {
        _sut.SetCurrentExecution(CreateExecution(ExecutionStatus.Failed));

        _sut.IsExecutionActive.Should().BeFalse();
    }

    [Fact]
    public void WhenExecutionIsPendingThenIsExecutionActiveIsTrue()
    {
        _sut.SetCurrentExecution(CreateExecution(ExecutionStatus.Pending));

        _sut.IsExecutionActive.Should().BeTrue();
    }

    [Fact]
    public void WhenSetCurrentExecutionThenFiresOnExecutionChanged()
    {
        ExecutionResponse? received = null;
        _sut.OnExecutionChanged += exec => received = exec;

        var execution = CreateExecution(ExecutionStatus.Running);
        _sut.SetCurrentExecution(execution);

        received.Should().Be(execution);
    }

    [Fact]
    public void WhenSetCurrentExecutionThenNotifiesStore()
    {
        var notified = false;
        _store.OnStateChanged += () => notified = true;

        _sut.SetCurrentExecution(CreateExecution(ExecutionStatus.Running));

        notified.Should().BeTrue();
    }

    [Fact]
    public void WhenUpdateExecutionWithMatchingIdThenUpdates()
    {
        var id = Guid.NewGuid();
        _sut.SetCurrentExecution(new ExecutionResponse { Id = id, Status = ExecutionStatus.Running });

        var updated = new ExecutionResponse { Id = id, Status = ExecutionStatus.Completed };
        _sut.UpdateExecution(updated);

        _sut.CurrentExecution!.Status.Should().Be(ExecutionStatus.Completed);
    }

    [Fact]
    public void WhenUpdateExecutionWithDifferentIdThenIgnored()
    {
        _sut.SetCurrentExecution(new ExecutionResponse { Id = Guid.NewGuid(), Status = ExecutionStatus.Running });

        _sut.UpdateExecution(new ExecutionResponse { Id = Guid.NewGuid(), Status = ExecutionStatus.Completed });

        _sut.CurrentExecution!.Status.Should().Be(ExecutionStatus.Running);
    }

    [Fact]
    public void WhenClearExecutionStateThenEverythingIsCleared()
    {
        _sut.SetCurrentExecution(CreateExecutionWithNodes());

        _sut.ClearExecutionState();

        _sut.CurrentExecution.Should().BeNull();
        _sut.GetAllNodeExecutionStates().Should().BeEmpty();
    }

    [Fact]
    public void WhenClearExecutionStateThenFiresOnExecutionChangedWithNull()
    {
        _sut.SetCurrentExecution(CreateExecution(ExecutionStatus.Running));
        ExecutionResponse? received = new ExecutionResponse();
        _sut.OnExecutionChanged += exec => received = exec;

        _sut.ClearExecutionState();

        received.Should().BeNull();
    }

    [Fact]
    public void WhenExecutionHasNodeExecutionsThenNodeStatesArePopulated()
    {
        _sut.SetCurrentExecution(CreateExecutionWithNodes());

        var state = _sut.GetNodeExecutionState("node-1");

        state.Should().NotBeNull();
        state!.NodeId.Should().Be("node-1");
        state.Status.Should().Be(ExecutionStatus.Completed);
    }

    [Fact]
    public void WhenGetNodeExecutionStateForUnknownNodeThenReturnsNull()
    {
        _sut.SetCurrentExecution(CreateExecutionWithNodes());

        _sut.GetNodeExecutionState("nonexistent").Should().BeNull();
    }

    [Fact]
    public void WhenSetNodeExecutionResultThenStateIsUpdated()
    {
        var result = new NodeExecutionResponse
        {
            NodeId = "n1",
            Status = ExecutionStatus.Completed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        _sut.SetNodeExecutionResult("n1", result);

        var state = _sut.GetNodeExecutionState("n1");
        state.Should().NotBeNull();
        state!.Status.Should().Be(ExecutionStatus.Completed);
    }

    [Fact]
    public void WhenMultipleExecutionsForSameNodeThenGroupedAsIterations()
    {
        var execution = new ExecutionResponse
        {
            Id = Guid.NewGuid(),
            Status = ExecutionStatus.Completed,
            NodeExecutions =
            [
                new NodeExecutionResponse { NodeId = "loop-body", Status = ExecutionStatus.Completed, StartedAt = DateTime.UtcNow },
                new NodeExecutionResponse { NodeId = "loop-body", Status = ExecutionStatus.Completed, StartedAt = DateTime.UtcNow },
                new NodeExecutionResponse { NodeId = "loop-body", Status = ExecutionStatus.Completed, StartedAt = DateTime.UtcNow }
            ]
        };

        _sut.SetCurrentExecution(execution);

        var state = _sut.GetNodeExecutionState("loop-body");
        state.Should().NotBeNull();
        state!.HasMultipleIterations.Should().BeTrue();
        state.IterationCount.Should().Be(3);
        state.Iterations.Should().HaveCount(3);
    }

    private static ExecutionResponse CreateExecution(ExecutionStatus status) => new()
    {
        Id = Guid.NewGuid(),
        Status = status,
        NodeExecutions = []
    };

    private static ExecutionResponse CreateExecutionWithNodes() => new()
    {
        Id = Guid.NewGuid(),
        Status = ExecutionStatus.Completed,
        NodeExecutions =
        [
            new NodeExecutionResponse
            {
                NodeId = "node-1",
                Status = ExecutionStatus.Completed,
                StartedAt = DateTime.UtcNow.AddSeconds(-1),
                CompletedAt = DateTime.UtcNow
            }
        ]
    };
}
