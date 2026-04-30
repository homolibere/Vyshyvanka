using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Engine.Credentials;
using Vyshyvanka.Engine.Execution;
using Vyshyvanka.Engine.Expressions;
using Vyshyvanka.Engine.Registry;

namespace Vyshyvanka.Tests.Unit;

public class WorkflowEngineTests
{
    private readonly NodeRegistry _nodeRegistry = new();
    private readonly IExpressionEvaluator _expressionEvaluator = new ExpressionEvaluator();
    private readonly WorkflowEngine _sut;

    public WorkflowEngineTests()
    {
        _nodeRegistry.Register<StubTriggerNode>();
        _nodeRegistry.Register<StubActionNode>();
        _nodeRegistry.Register<FailingActionNode>();
        _sut = new WorkflowEngine(_nodeRegistry, _expressionEvaluator);
    }

    private static Engine.Execution.ExecutionContext CreateContext() =>
        new(Guid.NewGuid(), Guid.NewGuid(), NullCredentialProvider.Instance);

    // --- Stub nodes ---

    private class StubTriggerNode : INode
    {
        public string Id => "stub-trigger-instance";
        public string Type => "stub-trigger";
        public NodeCategory Category => NodeCategory.Trigger;

        public Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
        {
            var data = JsonSerializer.SerializeToElement(new { triggered = true });
            return Task.FromResult(new NodeOutput { Data = data, Success = true });
        }
    }

    private class StubActionNode : INode
    {
        public string Id => "stub-action-instance";
        public string Type => "stub-action";
        public NodeCategory Category => NodeCategory.Action;

        public Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
        {
            var data = JsonSerializer.SerializeToElement(new { processed = true });
            return Task.FromResult(new NodeOutput { Data = data, Success = true });
        }
    }

    private class FailingActionNode : INode
    {
        public string Id => "failing-action-instance";
        public string Type => "failing-action";
        public NodeCategory Category => NodeCategory.Action;

        public Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
        {
            return Task.FromResult(new NodeOutput
            {
                Data = default,
                Success = false,
                ErrorMessage = "Node execution failed"
            });
        }
    }

    // --- Simple workflow execution ---

    [Fact]
    public async Task WhenExecutingSimpleWorkflowThenSucceeds()
    {
        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            Name = "Simple Workflow",
            Nodes =
            [
                new WorkflowNode { Id = "trigger", Type = "stub-trigger", Name = "Trigger" },
                new WorkflowNode { Id = "action", Type = "stub-action", Name = "Action" }
            ],
            Connections =
            [
                new Connection
                {
                    SourceNodeId = "trigger",
                    SourcePort = "output",
                    TargetNodeId = "action",
                    TargetPort = "input"
                }
            ]
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(workflow, context);

        result.Success.Should().BeTrue();
        result.NodeResults.Should().HaveCount(2);
    }

    [Fact]
    public async Task WhenExecutingWorkflowThenNodeOutputsAreStored()
    {
        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            Name = "Output Test",
            Nodes =
            [
                new WorkflowNode { Id = "trigger", Type = "stub-trigger", Name = "Trigger" }
            ],
            Connections = []
        };
        var context = CreateContext();

        await _sut.ExecuteAsync(workflow, context);

        context.NodeOutputs.HasOutput("trigger").Should().BeTrue();
    }

    // --- Error handling ---

    [Fact]
    public async Task WhenNodeFailsWithStopOnFirstErrorThenExecutionStops()
    {
        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            Name = "Failing Workflow",
            Settings = new WorkflowSettings
            {
                ErrorHandling = ErrorHandlingMode.StopOnFirstError
            },
            Nodes =
            [
                new WorkflowNode { Id = "trigger", Type = "stub-trigger", Name = "Trigger" },
                new WorkflowNode { Id = "fail", Type = "failing-action", Name = "Fail" },
                new WorkflowNode { Id = "after", Type = "stub-action", Name = "After" }
            ],
            Connections =
            [
                new Connection
                {
                    SourceNodeId = "trigger",
                    SourcePort = "output",
                    TargetNodeId = "fail",
                    TargetPort = "input"
                },
                new Connection
                {
                    SourceNodeId = "fail",
                    SourcePort = "output",
                    TargetNodeId = "after",
                    TargetPort = "input"
                }
            ]
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(workflow, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // --- Cancellation ---

    [Fact]
    public async Task WhenCancelledThenExecutionReturnsCancelled()
    {
        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            Name = "Cancel Test",
            Nodes =
            [
                new WorkflowNode { Id = "trigger", Type = "stub-trigger", Name = "Trigger" }
            ],
            Connections = []
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var context = new Engine.Execution.ExecutionContext(
            Guid.NewGuid(), Guid.NewGuid(), NullCredentialProvider.Instance, cts.Token);

        var result = await _sut.ExecuteAsync(workflow, context, cts.Token);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled");
    }

    // --- Cycle detection ---

    [Fact]
    public async Task WhenWorkflowHasCycleThenThrowsInvalidOperationException()
    {
        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            Name = "Cyclic Workflow",
            Nodes =
            [
                new WorkflowNode { Id = "a", Type = "stub-action", Name = "A" },
                new WorkflowNode { Id = "b", Type = "stub-action", Name = "B" }
            ],
            Connections =
            [
                new Connection { SourceNodeId = "a", TargetNodeId = "b" },
                new Connection { SourceNodeId = "b", TargetNodeId = "a" }
            ]
        };
        var context = CreateContext();

        var act = () => _sut.ExecuteAsync(workflow, context);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cycle*");
    }

    // --- Single node execution ---

    [Fact]
    public async Task WhenExecutingSingleNodeThenReturnsResult()
    {
        var node = new WorkflowNode
        {
            Id = "action-1",
            Type = "stub-action",
            Name = "Single Action"
        };
        var context = CreateContext();

        var result = await _sut.ExecuteNodeAsync(node, context);

        result.Success.Should().BeTrue();
    }

    // --- Parallel execution ---

    [Fact]
    public async Task WhenParallelBranchesExistThenAllExecute()
    {
        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            Name = "Parallel Workflow",
            Nodes =
            [
                new WorkflowNode { Id = "trigger", Type = "stub-trigger", Name = "Trigger" },
                new WorkflowNode { Id = "branch-a", Type = "stub-action", Name = "Branch A" },
                new WorkflowNode { Id = "branch-b", Type = "stub-action", Name = "Branch B" }
            ],
            Connections =
            [
                new Connection { SourceNodeId = "trigger", TargetNodeId = "branch-a" },
                new Connection { SourceNodeId = "trigger", TargetNodeId = "branch-b" }
            ]
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(workflow, context);

        result.Success.Should().BeTrue();
        result.NodeResults.Should().HaveCount(3);
    }

    // --- Null checks ---

    [Fact]
    public async Task WhenWorkflowIsNullThenThrowsArgumentNullException()
    {
        var context = CreateContext();

        var act = () => _sut.ExecuteAsync(null!, context);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task WhenContextIsNullThenThrowsArgumentNullException()
    {
        var workflow = new Workflow { Id = Guid.NewGuid(), Name = "Test" };

        var act = () => _sut.ExecuteAsync(workflow, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // --- Cancel execution ---

    [Fact]
    public async Task WhenCancelExecutionAsyncCalledThenCompletes()
    {
        // CancelExecutionAsync should not throw even for unknown IDs
        await _sut.CancelExecutionAsync(Guid.NewGuid());
    }
}
