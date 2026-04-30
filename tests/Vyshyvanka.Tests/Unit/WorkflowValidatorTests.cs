using Vyshyvanka.Core.Models;
using Vyshyvanka.Engine.Validation;

namespace Vyshyvanka.Tests.Unit;

public class WorkflowValidatorTests
{
    private readonly WorkflowValidator _sut = new();

    private static Workflow CreateValidWorkflow() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Workflow",
        Version = 1,
        IsActive = true,
        Nodes =
        [
            new WorkflowNode { Id = "trigger-1", Type = "manual-trigger", Name = "Start" },
            new WorkflowNode { Id = "action-1", Type = "http-request", Name = "Call API" }
        ],
        Connections =
        [
            new Connection
            {
                SourceNodeId = "trigger-1",
                SourcePort = "output",
                TargetNodeId = "action-1",
                TargetPort = "input"
            }
        ]
    };

    [Fact]
    public void WhenWorkflowIsValidThenValidationSucceeds()
    {
        var workflow = CreateValidWorkflow();

        var result = _sut.Validate(workflow);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void WhenWorkflowNameIsEmptyThenValidationFails()
    {
        var workflow = CreateValidWorkflow() with { Name = "" };

        var result = _sut.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "WORKFLOW_NAME_REQUIRED");
    }

    [Fact]
    public void WhenWorkflowNameExceedsMaxLengthThenValidationFails()
    {
        var workflow = CreateValidWorkflow() with { Name = new string('A', 201) };

        var result = _sut.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "WORKFLOW_NAME_TOO_LONG");
    }

    [Fact]
    public void WhenDescriptionExceedsMaxLengthThenValidationFails()
    {
        var workflow = CreateValidWorkflow() with { Description = new string('A', 2001) };

        var result = _sut.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "WORKFLOW_DESCRIPTION_TOO_LONG");
    }

    [Fact]
    public void WhenVersionIsNegativeThenValidationFails()
    {
        var workflow = CreateValidWorkflow() with { Version = -1 };

        var result = _sut.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "WORKFLOW_VERSION_INVALID");
    }

    [Fact]
    public void WhenNoTriggerNodeExistsThenValidationFails()
    {
        var workflow = CreateValidWorkflow() with
        {
            Nodes =
            [
                new WorkflowNode { Id = "action-1", Type = "http-request", Name = "Call API" }
            ],
            Connections = []
        };

        var result = _sut.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "WORKFLOW_TRIGGER_REQUIRED");
    }

    [Fact]
    public void WhenMultipleTriggerNodesExistThenValidationFails()
    {
        var workflow = CreateValidWorkflow() with
        {
            Nodes =
            [
                new WorkflowNode { Id = "trigger-1", Type = "manual-trigger", Name = "Start 1" },
                new WorkflowNode { Id = "trigger-2", Type = "webhook-trigger", Name = "Start 2" }
            ]
        };

        var result = _sut.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "WORKFLOW_MULTIPLE_TRIGGERS");
    }

    [Fact]
    public void WhenNodeIdIsDuplicateThenValidationFails()
    {
        var workflow = CreateValidWorkflow() with
        {
            Nodes =
            [
                new WorkflowNode { Id = "trigger-1", Type = "manual-trigger", Name = "Start" },
                new WorkflowNode { Id = "trigger-1", Type = "http-request", Name = "Duplicate" }
            ]
        };

        var result = _sut.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "NODE_ID_DUPLICATE");
    }

    [Fact]
    public void WhenNodeIdIsEmptyThenValidationFails()
    {
        var workflow = CreateValidWorkflow() with
        {
            Nodes =
            [
                new WorkflowNode { Id = "", Type = "manual-trigger", Name = "Start" }
            ]
        };

        var result = _sut.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "NODE_ID_REQUIRED");
    }

    [Fact]
    public void WhenNodeTypeIsEmptyThenValidationFails()
    {
        var workflow = CreateValidWorkflow() with
        {
            Nodes =
            [
                new WorkflowNode { Id = "node-1", Type = "", Name = "Bad Node" }
            ]
        };

        var result = _sut.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "NODE_TYPE_REQUIRED");
    }

    [Fact]
    public void WhenNodeNameIsEmptyThenValidationFails()
    {
        var workflow = CreateValidWorkflow() with
        {
            Nodes =
            [
                new WorkflowNode { Id = "trigger-1", Type = "manual-trigger", Name = "" }
            ]
        };

        var result = _sut.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "NODE_NAME_REQUIRED");
    }

    [Fact]
    public void WhenNodeNameExceedsMaxLengthThenValidationFails()
    {
        var workflow = CreateValidWorkflow() with
        {
            Nodes =
            [
                new WorkflowNode { Id = "trigger-1", Type = "manual-trigger", Name = new string('X', 201) }
            ]
        };

        var result = _sut.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "NODE_NAME_TOO_LONG");
    }

    [Fact]
    public void WhenConnectionSourceNodeDoesNotExistThenValidationFails()
    {
        var workflow = CreateValidWorkflow() with
        {
            Connections =
            [
                new Connection
                {
                    SourceNodeId = "nonexistent",
                    SourcePort = "output",
                    TargetNodeId = "action-1",
                    TargetPort = "input"
                }
            ]
        };

        var result = _sut.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "CONNECTION_SOURCE_NOT_FOUND");
    }

    [Fact]
    public void WhenConnectionTargetNodeDoesNotExistThenValidationFails()
    {
        var workflow = CreateValidWorkflow() with
        {
            Connections =
            [
                new Connection
                {
                    SourceNodeId = "trigger-1",
                    SourcePort = "output",
                    TargetNodeId = "nonexistent",
                    TargetPort = "input"
                }
            ]
        };

        var result = _sut.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "CONNECTION_TARGET_NOT_FOUND");
    }

    [Fact]
    public void WhenConnectionIsSelfLoopThenValidationFails()
    {
        var workflow = CreateValidWorkflow() with
        {
            Connections =
            [
                new Connection
                {
                    SourceNodeId = "action-1",
                    SourcePort = "output",
                    TargetNodeId = "action-1",
                    TargetPort = "input"
                }
            ]
        };

        var result = _sut.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "CONNECTION_SELF_LOOP");
    }

    [Fact]
    public void WhenConnectionSourcePortIsEmptyThenValidationFails()
    {
        var workflow = CreateValidWorkflow() with
        {
            Connections =
            [
                new Connection
                {
                    SourceNodeId = "trigger-1",
                    SourcePort = "",
                    TargetNodeId = "action-1",
                    TargetPort = "input"
                }
            ]
        };

        var result = _sut.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "CONNECTION_SOURCE_PORT_REQUIRED");
    }

    [Fact]
    public void WhenWorkflowIsNullThenThrowsArgumentNullException()
    {
        var act = () => _sut.Validate(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WhenMultipleErrorsExistThenAllAreReported()
    {
        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            Name = "",
            Version = -1,
            Nodes = [],
            Connections = []
        };

        var result = _sut.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().BeGreaterThan(1);
    }
}
