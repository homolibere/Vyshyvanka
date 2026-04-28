using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Registry;

namespace Vyshyvanka.Tests.Unit;

public class NodeRegistryTests
{
    [Fact]
    public void WhenNodeIsRegisteredThenItCanBeRetrieved()
    {
        // Arrange
        var registry = new NodeRegistry();
        
        // Act
        registry.Register<TestNode>();
        
        // Assert
        Assert.True(registry.IsRegistered("test-node"));
    }

    [Fact]
    public void WhenNodeIsRegisteredThenDefinitionIsAvailable()
    {
        // Arrange
        var registry = new NodeRegistry();
        registry.Register<TestNode>();
        
        // Act
        var definition = registry.GetDefinition("test-node");
        
        // Assert
        Assert.NotNull(definition);
        Assert.Equal("test-node", definition.Type);
        Assert.Equal(NodeCategory.Action, definition.Category);
    }

    [Fact]
    public void WhenNodeIsNotRegisteredThenCreateNodeThrows()
    {
        // Arrange
        var registry = new NodeRegistry();
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            registry.CreateNode("unknown-node", default));
    }

    [Fact]
    public void WhenGetAllDefinitionsThenReturnsAllRegistered()
    {
        // Arrange
        var registry = new NodeRegistry();
        registry.Register<TestNode>();
        
        // Act
        var definitions = registry.GetAllDefinitions().ToList();
        
        // Assert
        Assert.Single(definitions);
    }
}

/// <summary>
/// Test node for unit testing.
/// </summary>
internal class TestNode : INode
{
    public string Id => "test-1";
    public string Type => "test-node";
    public NodeCategory Category => NodeCategory.Action;

    public Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        return Task.FromResult(new NodeOutput
        {
            Data = JsonDocument.Parse("{}").RootElement,
            Success = true
        });
    }
}
