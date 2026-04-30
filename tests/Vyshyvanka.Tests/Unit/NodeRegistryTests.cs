using System.Text.Json;
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Registry;

namespace Vyshyvanka.Tests.Unit;

public class NodeRegistryTests
{
    private readonly NodeRegistry _sut = new();

    [NodeDefinition(Name = "Test Action", Description = "A test action node")]
    private class TestActionNode : INode
    {
        public string Id => "test-action-instance";
        public string Type => "test-action";
        public NodeCategory Category => NodeCategory.Action;

        public Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
        {
            return Task.FromResult(new NodeOutput { Data = default, Success = true });
        }
    }

    [NodeDefinition(Name = "Test Trigger", Description = "A test trigger node")]
    private class TestTriggerNode : INode
    {
        public string Id => "test-trigger-instance";
        public string Type => "test-trigger";
        public NodeCategory Category => NodeCategory.Trigger;

        public Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
        {
            return Task.FromResult(new NodeOutput { Data = default, Success = true });
        }
    }

    [Fact]
    public void WhenRegisteringNodeTypeThenIsRegisteredReturnsTrue()
    {
        _sut.Register<TestActionNode>();

        _sut.IsRegistered("test-action").Should().BeTrue();
    }

    [Fact]
    public void WhenNodeNotRegisteredThenIsRegisteredReturnsFalse()
    {
        _sut.IsRegistered("nonexistent").Should().BeFalse();
    }

    [Fact]
    public void WhenCreatingRegisteredNodeThenReturnsInstance()
    {
        _sut.Register<TestActionNode>();

        var node = _sut.CreateNode("test-action", default);

        node.Should().NotBeNull();
        node.Type.Should().Be("test-action");
        node.Category.Should().Be(NodeCategory.Action);
    }

    [Fact]
    public void WhenCreatingUnregisteredNodeThenThrowsInvalidOperationException()
    {
        var act = () => _sut.CreateNode("nonexistent", default);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void WhenGettingDefinitionThenReturnsMetadata()
    {
        _sut.Register<TestActionNode>();

        var definition = _sut.GetDefinition("test-action");

        definition.Should().NotBeNull();
        definition!.Name.Should().Be("Test Action");
        definition.Description.Should().Be("A test action node");
        definition.Category.Should().Be(NodeCategory.Action);
    }

    [Fact]
    public void WhenGettingDefinitionForUnregisteredTypeThenReturnsNull()
    {
        var definition = _sut.GetDefinition("nonexistent");

        definition.Should().BeNull();
    }

    [Fact]
    public void WhenGettingAllDefinitionsThenReturnsAll()
    {
        _sut.Register<TestActionNode>();
        _sut.Register<TestTriggerNode>();

        var definitions = _sut.GetAllDefinitions().ToList();

        definitions.Should().HaveCount(2);
    }

    [Fact]
    public void WhenUnregisteringNodeThenIsNoLongerRegistered()
    {
        _sut.Register<TestActionNode>();

        var removed = _sut.Unregister("test-action");

        removed.Should().BeTrue();
        _sut.IsRegistered("test-action").Should().BeFalse();
    }

    [Fact]
    public void WhenUnregisteringNonexistentNodeThenReturnsFalse()
    {
        var removed = _sut.Unregister("nonexistent");

        removed.Should().BeFalse();
    }

    [Fact]
    public void WhenRegisteringByTypeThenWorks()
    {
        _sut.Register(typeof(TestActionNode));

        _sut.IsRegistered("test-action").Should().BeTrue();
    }

    [Fact]
    public void WhenRegisteringNonNodeTypeThenThrowsArgumentException()
    {
        var act = () => _sut.Register(typeof(string));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WhenCreatingNodeWithEmptyTypeThenThrowsArgumentException()
    {
        var act = () => _sut.CreateNode("", default);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WhenNodeTypeIsCaseInsensitiveThenMatchesCorrectly()
    {
        _sut.Register<TestActionNode>();

        _sut.IsRegistered("TEST-ACTION").Should().BeTrue();
        _sut.IsRegistered("Test-Action").Should().BeTrue();
    }
}
