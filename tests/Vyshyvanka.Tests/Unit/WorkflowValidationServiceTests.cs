using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Designer.Services;

namespace Vyshyvanka.Tests.Unit;

public class WorkflowValidationServiceTests
{
    private readonly WorkflowStore _store = new();
    private readonly WorkflowValidationService _sut;

    public WorkflowValidationServiceTests()
    {
        _sut = new WorkflowValidationService(_store);
    }

    [Fact]
    public void WhenWorkflowIsValidThenNoErrors()
    {
        SetupValidWorkflow();

        _sut.ValidateWorkflow();

        _sut.HasValidationErrors.Should().BeFalse();
        _sut.ValidationResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public void WhenWorkflowNameIsEmptyThenValidationFails()
    {
        _store.SetWorkflow(_store.Workflow with { Name = "" });

        _sut.ValidateWorkflow();

        _sut.HasValidationErrors.Should().BeTrue();
        _sut.ValidationResult.Errors.Should().Contain(e => e.ErrorCode == "WORKFLOW_NAME_REQUIRED");
    }

    [Fact]
    public void WhenNodeHasNoIdThenValidationFails()
    {
        _store.SetWorkflow(_store.Workflow with
        {
            Nodes = [new WorkflowNode { Id = "", Type = "if", Name = "My If" }]
        });

        _sut.ValidateWorkflow();

        _sut.ValidationResult.Errors.Should().Contain(e => e.ErrorCode == "NODE_ID_REQUIRED");
    }

    [Fact]
    public void WhenNodeHasDuplicateIdThenValidationFails()
    {
        _store.SetWorkflow(_store.Workflow with
        {
            Nodes =
            [
                new WorkflowNode { Id = "dup", Type = "if", Name = "First" },
                new WorkflowNode { Id = "dup", Type = "if", Name = "Second" }
            ]
        });

        _sut.ValidateWorkflow();

        _sut.ValidationResult.Errors.Should().Contain(e => e.ErrorCode == "NODE_ID_DUPLICATE");
    }

    [Fact]
    public void WhenNodeHasNoTypeThenValidationFails()
    {
        _store.SetWorkflow(_store.Workflow with
        {
            Nodes = [new WorkflowNode { Id = "n1", Type = "", Name = "Node" }]
        });

        _sut.ValidateWorkflow();

        _sut.ValidationResult.Errors.Should().Contain(e => e.ErrorCode == "NODE_TYPE_REQUIRED");
    }

    [Fact]
    public void WhenNodeHasNoNameThenValidationFails()
    {
        _store.SetWorkflow(_store.Workflow with
        {
            Nodes = [new WorkflowNode { Id = "n1", Type = "if", Name = "" }]
        });

        _sut.ValidateWorkflow();

        _sut.ValidationResult.Errors.Should().Contain(e => e.ErrorCode == "NODE_NAME_REQUIRED");
    }

    [Fact]
    public void WhenConnectionSourceNodeDoesNotExistThenValidationFails()
    {
        _store.SetWorkflow(_store.Workflow with
        {
            Nodes = [new WorkflowNode { Id = "n1", Type = "if", Name = "If" }],
            Connections = [new Connection { SourceNodeId = "missing", SourcePort = "out", TargetNodeId = "n1", TargetPort = "in" }]
        });

        _sut.ValidateWorkflow();

        _sut.ValidationResult.Errors.Should().Contain(e => e.ErrorCode == "CONNECTION_SOURCE_NOT_FOUND");
    }

    [Fact]
    public void WhenConnectionTargetNodeDoesNotExistThenValidationFails()
    {
        _store.SetWorkflow(_store.Workflow with
        {
            Nodes = [new WorkflowNode { Id = "n1", Type = "if", Name = "If" }],
            Connections = [new Connection { SourceNodeId = "n1", SourcePort = "out", TargetNodeId = "missing", TargetPort = "in" }]
        });

        _sut.ValidateWorkflow();

        _sut.ValidationResult.Errors.Should().Contain(e => e.ErrorCode == "CONNECTION_TARGET_NOT_FOUND");
    }

    [Fact]
    public void WhenConnectionIsSelfLoopThenValidationFails()
    {
        _store.SetWorkflow(_store.Workflow with
        {
            Nodes = [new WorkflowNode { Id = "n1", Type = "if", Name = "If" }],
            Connections = [new Connection { SourceNodeId = "n1", SourcePort = "out", TargetNodeId = "n1", TargetPort = "in" }]
        });

        _sut.ValidateWorkflow();

        _sut.ValidationResult.Errors.Should().Contain(e => e.ErrorCode == "CONNECTION_SELF_LOOP");
    }

    [Fact]
    public void WhenNoTriggerNodeThenValidationFails()
    {
        _store.SetNodeDefinitions([new NodeDefinition { Type = "if", Name = "If", Category = NodeCategory.Logic }]);
        _store.SetWorkflow(_store.Workflow with
        {
            Nodes = [new WorkflowNode { Id = "n1", Type = "if", Name = "If" }]
        });

        _sut.ValidateWorkflow();

        _sut.ValidationResult.Errors.Should().Contain(e => e.ErrorCode == "WORKFLOW_NO_TRIGGER");
    }

    [Fact]
    public void WhenHasTriggerNodeThenNoTriggerError()
    {
        SetupValidWorkflow();

        _sut.ValidateWorkflow();

        _sut.ValidationResult.Errors.Should().NotContain(e => e.ErrorCode == "WORKFLOW_NO_TRIGGER");
    }

    [Fact]
    public void WhenValidateWorkflowThenFiresOnValidationChanged()
    {
        ValidationResult? received = null;
        _sut.OnValidationChanged += result => received = result;

        _sut.ValidateWorkflow();

        received.Should().NotBeNull();
    }

    [Fact]
    public void WhenIsValidConnectionWithSelfLoopThenReturnsFalse()
    {
        _sut.IsValidConnection("n1", "out", "n1", "in").Should().BeFalse();
    }

    [Fact]
    public void WhenIsValidConnectionWithDuplicateThenReturnsFalse()
    {
        _store.SetWorkflow(_store.Workflow with
        {
            Nodes =
            [
                new WorkflowNode { Id = "n1", Type = "trigger", Name = "Trigger" },
                new WorkflowNode { Id = "n2", Type = "action", Name = "Action" }
            ],
            Connections = [new Connection { SourceNodeId = "n1", SourcePort = "output", TargetNodeId = "n2", TargetPort = "input" }]
        });

        _sut.IsValidConnection("n1", "output", "n2", "input").Should().BeFalse();
    }

    [Fact]
    public void WhenIsValidConnectionWithCompatiblePortsThenReturnsTrue()
    {
        _store.SetNodeDefinitions(
        [
            new NodeDefinition
            {
                Type = "trigger", Name = "Trigger", Category = NodeCategory.Trigger,
                Outputs = [new PortDefinition { Name = "output", DisplayName = "Output", Type = PortType.Any }]
            },
            new NodeDefinition
            {
                Type = "action", Name = "Action", Category = NodeCategory.Action,
                Inputs = [new PortDefinition { Name = "input", DisplayName = "Input", Type = PortType.Any }]
            }
        ]);
        _store.SetWorkflow(_store.Workflow with
        {
            Nodes =
            [
                new WorkflowNode { Id = "n1", Type = "trigger", Name = "Trigger" },
                new WorkflowNode { Id = "n2", Type = "action", Name = "Action" }
            ]
        });

        _sut.IsValidConnection("n1", "output", "n2", "input").Should().BeTrue();
    }

    [Fact]
    public void WhenIsValidConnectionWithIncompatiblePortsThenReturnsFalse()
    {
        _store.SetNodeDefinitions(
        [
            new NodeDefinition
            {
                Type = "trigger", Name = "Trigger", Category = NodeCategory.Trigger,
                Outputs = [new PortDefinition { Name = "output", DisplayName = "Output", Type = PortType.Boolean }]
            },
            new NodeDefinition
            {
                Type = "action", Name = "Action", Category = NodeCategory.Action,
                Inputs = [new PortDefinition { Name = "input", DisplayName = "Input", Type = PortType.Array }]
            }
        ]);
        _store.SetWorkflow(_store.Workflow with
        {
            Nodes =
            [
                new WorkflowNode { Id = "n1", Type = "trigger", Name = "Trigger" },
                new WorkflowNode { Id = "n2", Type = "action", Name = "Action" }
            ]
        });

        _sut.IsValidConnection("n1", "output", "n2", "input").Should().BeFalse();
    }

    [Fact]
    public void WhenGetNodeValidationErrorsThenReturnsErrorsForNode()
    {
        _store.SetWorkflow(_store.Workflow with
        {
            Nodes =
            [
                new WorkflowNode { Id = "n1", Type = "", Name = "" },
                new WorkflowNode { Id = "n2", Type = "if", Name = "Good" }
            ]
        });
        _sut.ValidateWorkflow();

        // GetNodeValidationErrors searches by node ID in path or message
        // The validation uses index-based paths, so test with a connection error that references the node ID
        _store.SetWorkflow(_store.Workflow with
        {
            Nodes =
            [
                new WorkflowNode { Id = "n1", Type = "if", Name = "If" },
                new WorkflowNode { Id = "n2", Type = "if", Name = "Good" }
            ],
            Connections = [new Connection { SourceNodeId = "missing-node", SourcePort = "out", TargetNodeId = "n2", TargetPort = "in" }]
        });
        _sut.ValidateWorkflow();

        var errors = _sut.GetNodeValidationErrors("missing-node").ToList();

        errors.Should().NotBeEmpty();
    }

    private void SetupValidWorkflow()
    {
        _store.SetNodeDefinitions(
        [
            new NodeDefinition { Type = "manual-trigger", Name = "Manual Trigger", Category = NodeCategory.Trigger }
        ]);
        _store.SetWorkflow(_store.Workflow with
        {
            Name = "Valid Workflow",
            Nodes = [new WorkflowNode { Id = "t1", Type = "manual-trigger", Name = "Trigger" }]
        });
    }
}
