using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Designer.Services;

namespace Vyshyvanka.Tests.Unit;

public class WorkflowStoreTests
{
    private readonly WorkflowStore _sut = new();

    [Fact]
    public void WhenCreatedThenHasEmptyWorkflow()
    {
        _sut.Workflow.Should().NotBeNull();
        _sut.Workflow.Name.Should().Be("New Workflow");
        _sut.Workflow.Nodes.Should().BeEmpty();
        _sut.Workflow.Connections.Should().BeEmpty();
    }

    [Fact]
    public void WhenCreatedThenIsNotDirty()
    {
        _sut.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void WhenMarkDirtyThenIsDirtyIsTrue()
    {
        _sut.MarkDirty();

        _sut.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void WhenMarkAsSavedThenIsDirtyIsFalse()
    {
        _sut.MarkDirty();

        _sut.MarkAsSaved();

        _sut.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void WhenMarkAsSavedThenNotifiesStateChanged()
    {
        var notified = false;
        _sut.OnStateChanged += () => notified = true;

        _sut.MarkAsSaved();

        notified.Should().BeTrue();
    }

    [Fact]
    public void WhenSetNodeDefinitionsThenDefinitionsAreAvailable()
    {
        var definitions = new List<NodeDefinition>
        {
            new() { Type = "http-request", Name = "HTTP Request", Category = NodeCategory.Action },
            new() { Type = "if", Name = "If", Category = NodeCategory.Logic }
        };

        _sut.SetNodeDefinitions(definitions);

        _sut.NodeDefinitions.Should().HaveCount(2);
    }

    [Fact]
    public void WhenGetNodeDefinitionThenReturnsMatchingDefinition()
    {
        _sut.SetNodeDefinitions([new NodeDefinition { Type = "if", Name = "If", Category = NodeCategory.Logic }]);

        var result = _sut.GetNodeDefinition("if");

        result.Should().NotBeNull();
        result!.Name.Should().Be("If");
    }

    [Fact]
    public void WhenGetNodeDefinitionWithUnknownTypeThenReturnsNull()
    {
        _sut.GetNodeDefinition("nonexistent").Should().BeNull();
    }

    [Fact]
    public void WhenGetNodeThenReturnsMatchingNode()
    {
        var workflow = _sut.Workflow with
        {
            Nodes = [new WorkflowNode { Id = "n1", Type = "if", Name = "My If" }]
        };
        _sut.SetWorkflow(workflow);

        var result = _sut.GetNode("n1");

        result.Should().NotBeNull();
        result!.Name.Should().Be("My If");
    }

    [Fact]
    public void WhenGetNodeWithUnknownIdThenReturnsNull()
    {
        _sut.GetNode("nonexistent").Should().BeNull();
    }

    [Fact]
    public void WhenSerializeToJsonThenProducesValidJson()
    {
        var json = _sut.SerializeToJson();

        json.Should().Contain("\"name\"");
        json.Should().Contain("New Workflow");
    }

    [Fact]
    public void WhenDeserializeFromJsonThenRoundTrips()
    {
        var json = _sut.SerializeToJson();

        var result = WorkflowStore.DeserializeFromJson(json);

        result.Should().NotBeNull();
        result!.Name.Should().Be("New Workflow");
    }

    [Fact]
    public void WhenDeserializeFromInvalidJsonThenReturnsNull()
    {
        WorkflowStore.DeserializeFromJson("not json").Should().BeNull();
    }

    [Fact]
    public void WhenDeserializeFromEmptyStringThenReturnsNull()
    {
        WorkflowStore.DeserializeFromJson("").Should().BeNull();
    }
}
