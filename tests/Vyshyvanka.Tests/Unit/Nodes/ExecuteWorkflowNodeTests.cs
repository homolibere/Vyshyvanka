using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Engine.Credentials;
using Vyshyvanka.Engine.Nodes.Actions;

namespace Vyshyvanka.Tests.Unit.Nodes;

public class ExecuteWorkflowNodeTests
{
    private readonly ExecuteWorkflowNode _sut = new();

    private static Engine.Execution.ExecutionContext CreateContext(
        IServiceProvider? services = null,
        Guid? userId = null,
        Guid? workflowId = null) =>
        new(Guid.NewGuid(),
            workflowId ?? Guid.NewGuid(),
            NullCredentialProvider.Instance,
            services: services,
            userId: userId);

    [Fact]
    public void WhenCreatedThenHasCorrectMetadata()
    {
        _sut.Type.Should().Be("execute-workflow");
        _sut.Category.Should().Be(NodeCategory.Action);
        _sut.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task WhenWorkflowIdIsInvalidGuidThenReturnsFailure()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            workflowId = "not-a-guid"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid workflow ID");
    }

    [Fact]
    public async Task WhenWorkflowExecutesItselfThenReturnsFailure()
    {
        var workflowId = Guid.NewGuid();
        var config = JsonSerializer.SerializeToElement(new
        {
            workflowId = workflowId.ToString()
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext(workflowId: workflowId);

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cannot execute itself");
    }

    [Fact]
    public async Task WhenServiceProviderIsNullThenReturnsFailure()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            workflowId = Guid.NewGuid().ToString()
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext(services: null);

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Service provider is not available");
    }

    [Fact]
    public async Task WhenRequiredServicesNotRegisteredThenReturnsFailure()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var config = JsonSerializer.SerializeToElement(new
        {
            workflowId = Guid.NewGuid().ToString()
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext(services: services);

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Required services");
    }

    [Fact]
    public async Task WhenTargetWorkflowNotFoundThenReturnsFailure()
    {
        var targetWorkflowId = Guid.NewGuid();
        var workflowRepo = Substitute.For<IWorkflowRepository>();
        workflowRepo.GetByIdAsync(targetWorkflowId, Arg.Any<CancellationToken>())
            .Returns((Workflow?)null);

        var workflowEngine = Substitute.For<IWorkflowEngine>();

        var services = new ServiceCollection()
            .AddSingleton(workflowRepo)
            .AddSingleton(workflowEngine)
            .BuildServiceProvider();

        var config = JsonSerializer.SerializeToElement(new
        {
            workflowId = targetWorkflowId.ToString()
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext(services: services);

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task WhenTargetWorkflowIsInactiveThenReturnsFailure()
    {
        var targetWorkflowId = Guid.NewGuid();
        var workflow = new Workflow
        {
            Id = targetWorkflowId,
            Name = "Inactive Workflow",
            IsActive = false
        };

        var workflowRepo = Substitute.For<IWorkflowRepository>();
        workflowRepo.GetByIdAsync(targetWorkflowId, Arg.Any<CancellationToken>())
            .Returns(workflow);

        var workflowEngine = Substitute.For<IWorkflowEngine>();

        var services = new ServiceCollection()
            .AddSingleton(workflowRepo)
            .AddSingleton(workflowEngine)
            .BuildServiceProvider();

        var config = JsonSerializer.SerializeToElement(new
        {
            workflowId = targetWorkflowId.ToString()
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext(services: services);

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not active");
    }

    [Fact]
    public async Task WhenUserDoesNotOwnTargetWorkflowThenReturnsFailure()
    {
        var targetWorkflowId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var workflow = new Workflow
        {
            Id = targetWorkflowId,
            Name = "Other User Workflow",
            IsActive = true,
            CreatedBy = ownerId
        };

        var workflowRepo = Substitute.For<IWorkflowRepository>();
        workflowRepo.GetByIdAsync(targetWorkflowId, Arg.Any<CancellationToken>())
            .Returns(workflow);

        var workflowEngine = Substitute.For<IWorkflowEngine>();

        var services = new ServiceCollection()
            .AddSingleton(workflowRepo)
            .AddSingleton(workflowEngine)
            .BuildServiceProvider();

        var config = JsonSerializer.SerializeToElement(new
        {
            workflowId = targetWorkflowId.ToString()
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext(services: services, userId: currentUserId);

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Access denied");
    }

    [Fact]
    public async Task WhenChildWorkflowSucceedsThenReturnsSuccessWithOutput()
    {
        var targetWorkflowId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var workflow = new Workflow
        {
            Id = targetWorkflowId,
            Name = "Child Workflow",
            IsActive = true,
            CreatedBy = currentUserId
        };

        var workflowRepo = Substitute.For<IWorkflowRepository>();
        workflowRepo.GetByIdAsync(targetWorkflowId, Arg.Any<CancellationToken>())
            .Returns(workflow);

        var executionResult = new ExecutionResult
        {
            Success = true,
            OutputData = JsonSerializer.SerializeToElement(new { result = "done" }),
            Duration = TimeSpan.FromMilliseconds(150),
            NodeResults = [new NodeExecutionResult { NodeId = "node1", Success = true }]
        };

        var workflowEngine = Substitute.For<IWorkflowEngine>();
        workflowEngine.ExecuteAsync(workflow, Arg.Any<IExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(executionResult);

        var services = new ServiceCollection()
            .AddSingleton(workflowRepo)
            .AddSingleton(workflowEngine)
            .BuildServiceProvider();

        var config = JsonSerializer.SerializeToElement(new
        {
            workflowId = targetWorkflowId.ToString()
        });
        var inputData = new { param1 = "value1" };
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(inputData),
            Configuration = config
        };
        var context = CreateContext(services: services, userId: currentUserId);

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("success").GetBoolean().Should().BeTrue();
        result.Data.GetProperty("workflowName").GetString().Should().Be("Child Workflow");
        result.Data.GetProperty("nodeCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task WhenChildWorkflowFailsThenReturnsFailure()
    {
        var targetWorkflowId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var workflow = new Workflow
        {
            Id = targetWorkflowId,
            Name = "Failing Child",
            IsActive = true,
            CreatedBy = currentUserId
        };

        var workflowRepo = Substitute.For<IWorkflowRepository>();
        workflowRepo.GetByIdAsync(targetWorkflowId, Arg.Any<CancellationToken>())
            .Returns(workflow);

        var executionResult = new ExecutionResult
        {
            Success = false,
            ErrorMessage = "Node X failed",
            Duration = TimeSpan.FromMilliseconds(50),
            NodeResults = []
        };

        var workflowEngine = Substitute.For<IWorkflowEngine>();
        workflowEngine.ExecuteAsync(workflow, Arg.Any<IExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(executionResult);

        var services = new ServiceCollection()
            .AddSingleton(workflowRepo)
            .AddSingleton(workflowEngine)
            .BuildServiceProvider();

        var config = JsonSerializer.SerializeToElement(new
        {
            workflowId = targetWorkflowId.ToString()
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext(services: services, userId: currentUserId);

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failing Child");
        result.ErrorMessage.Should().Contain("failed");
    }

    [Fact]
    public async Task WhenNoUserContextThenSkipsOwnershipCheck()
    {
        var targetWorkflowId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var workflow = new Workflow
        {
            Id = targetWorkflowId,
            Name = "Any User Workflow",
            IsActive = true,
            CreatedBy = otherUserId
        };

        var workflowRepo = Substitute.For<IWorkflowRepository>();
        workflowRepo.GetByIdAsync(targetWorkflowId, Arg.Any<CancellationToken>())
            .Returns(workflow);

        var executionResult = new ExecutionResult
        {
            Success = true,
            Duration = TimeSpan.FromMilliseconds(10),
            NodeResults = []
        };

        var workflowEngine = Substitute.For<IWorkflowEngine>();
        workflowEngine.ExecuteAsync(workflow, Arg.Any<IExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(executionResult);

        var services = new ServiceCollection()
            .AddSingleton(workflowRepo)
            .AddSingleton(workflowEngine)
            .BuildServiceProvider();

        var config = JsonSerializer.SerializeToElement(new
        {
            workflowId = targetWorkflowId.ToString()
        });
        var input = new NodeInput { Data = default, Configuration = config };
        // No userId — simulates webhook/system execution
        var context = CreateContext(services: services, userId: null);

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
    }
}
