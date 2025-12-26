using System.Text.Json;
using CsCheck;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Engine.Nodes.Logic;
using FlowForge.Engine.Nodes.Triggers;
using FlowForge.Engine.Registry;

namespace FlowForge.Tests.Property;

/// <summary>
/// Property-based tests for node registration and metadata.
/// Feature: flowforge, Property 4: Node Registration and Metadata Completeness
/// </summary>
public class NodeRegistryPropertyTests
{
    /// <summary>
    /// Feature: flowforge, Property 4: Node Registration and Metadata Completeness
    /// For any node registered in the Node_Registry (whether built-in or custom), 
    /// the registry SHALL provide complete metadata including name, description, 
    /// inputs, outputs, and configuration schema.
    /// Validates: Requirements 2.6, 2.7
    /// </summary>
    [Fact]
    public void RegisteredNode_HasCompleteMetadata()
    {
        // Create generators for different node types to register
        var nodeTypes = GetAllTestNodeTypes();
        
        Gen.OneOfConst(nodeTypes.ToArray()).Sample(nodeType =>
        {
            // Arrange
            var registry = new NodeRegistry();
            
            // Act - Register the node type using the generic Register<TNode>() method
            var registerMethod = typeof(NodeRegistry)
                .GetMethods()
                .First(m => m.Name == nameof(NodeRegistry.Register) && m.IsGenericMethod)
                .MakeGenericMethod(nodeType);
            registerMethod.Invoke(registry, null);
            
            // Get the node instance to retrieve its Type property
            var nodeInstance = (INode)Activator.CreateInstance(nodeType)!;
            var definition = registry.GetDefinition(nodeInstance.Type);
            
            // Assert - Verify complete metadata
            Assert.NotNull(definition);
            
            // Type must be non-empty and match the node's Type property
            Assert.False(string.IsNullOrWhiteSpace(definition.Type), 
                $"Node {nodeType.Name}: Type must not be empty");
            Assert.Equal(nodeInstance.Type, definition.Type);
            
            // Name must be non-empty
            Assert.False(string.IsNullOrWhiteSpace(definition.Name), 
                $"Node {nodeType.Name}: Name must not be empty");
            
            // Description must not be null (can be empty)
            Assert.NotNull(definition.Description);
            
            // Category must be a valid enum value
            Assert.True(Enum.IsDefined(typeof(NodeCategory), definition.Category),
                $"Node {nodeType.Name}: Category must be a valid NodeCategory");
            
            // Icon must be non-empty
            Assert.False(string.IsNullOrWhiteSpace(definition.Icon),
                $"Node {nodeType.Name}: Icon must not be empty");
            
            // Inputs must be non-empty with valid port definitions
            Assert.NotNull(definition.Inputs);
            Assert.NotEmpty(definition.Inputs);
            foreach (var input in definition.Inputs)
            {
                Assert.False(string.IsNullOrWhiteSpace(input.Name),
                    $"Node {nodeType.Name}: Input port Name must not be empty");
                Assert.False(string.IsNullOrWhiteSpace(input.DisplayName),
                    $"Node {nodeType.Name}: Input port DisplayName must not be empty");
                Assert.True(Enum.IsDefined(typeof(PortType), input.Type),
                    $"Node {nodeType.Name}: Input port Type must be valid");
            }
            
            // Outputs must be non-empty with valid port definitions
            Assert.NotNull(definition.Outputs);
            Assert.NotEmpty(definition.Outputs);
            foreach (var output in definition.Outputs)
            {
                Assert.False(string.IsNullOrWhiteSpace(output.Name),
                    $"Node {nodeType.Name}: Output port Name must not be empty");
                Assert.False(string.IsNullOrWhiteSpace(output.DisplayName),
                    $"Node {nodeType.Name}: Output port DisplayName must not be empty");
                Assert.True(Enum.IsDefined(typeof(PortType), output.Type),
                    $"Node {nodeType.Name}: Output port Type must be valid");
            }
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 4: Node Registration and Metadata Completeness
    /// For any node registered from an assembly, the registry SHALL provide 
    /// complete metadata for all discovered nodes.
    /// Validates: Requirements 2.6, 2.7
    /// </summary>
    [Fact]
    public void RegisterFromAssembly_AllNodesHaveCompleteMetadata()
    {
        // Arrange
        var registry = new NodeRegistry();
        var engineAssembly = typeof(ManualTriggerNode).Assembly;
        
        // Act
        registry.RegisterFromAssembly(engineAssembly);
        
        // Assert - All registered nodes have complete metadata
        var definitions = registry.GetAllDefinitions().ToList();
        
        Assert.NotEmpty(definitions);
        
        foreach (var definition in definitions)
        {
            // Type must be non-empty
            Assert.False(string.IsNullOrWhiteSpace(definition.Type),
                $"Definition Type must not be empty");
            
            // Name must be non-empty
            Assert.False(string.IsNullOrWhiteSpace(definition.Name),
                $"Node {definition.Type}: Name must not be empty");
            
            // Description must not be null
            Assert.NotNull(definition.Description);
            
            // Category must be valid
            Assert.True(Enum.IsDefined(typeof(NodeCategory), definition.Category),
                $"Node {definition.Type}: Category must be valid");
            
            // Icon must be non-empty
            Assert.False(string.IsNullOrWhiteSpace(definition.Icon),
                $"Node {definition.Type}: Icon must not be empty");
            
            // Inputs must be valid
            Assert.NotNull(definition.Inputs);
            Assert.NotEmpty(definition.Inputs);
            Assert.All(definition.Inputs, input =>
            {
                Assert.False(string.IsNullOrWhiteSpace(input.Name));
                Assert.False(string.IsNullOrWhiteSpace(input.DisplayName));
                Assert.True(Enum.IsDefined(typeof(PortType), input.Type));
            });
            
            // Outputs must be valid
            Assert.NotNull(definition.Outputs);
            Assert.NotEmpty(definition.Outputs);
            Assert.All(definition.Outputs, output =>
            {
                Assert.False(string.IsNullOrWhiteSpace(output.Name));
                Assert.False(string.IsNullOrWhiteSpace(output.DisplayName));
                Assert.True(Enum.IsDefined(typeof(PortType), output.Type));
            });
        }
    }

    /// <summary>
    /// Feature: flowforge, Property 4: Node Registration and Metadata Completeness
    /// For any registered node, the node can be created and its Type matches the definition.
    /// Validates: Requirements 2.6, 2.7
    /// </summary>
    [Fact]
    public void RegisteredNode_CanBeCreatedAndTypeMatches()
    {
        var nodeTypes = GetAllTestNodeTypes();
        
        Gen.OneOfConst(nodeTypes.ToArray()).Sample(nodeType =>
        {
            // Arrange
            var registry = new NodeRegistry();
            var registerMethod = typeof(NodeRegistry)
                .GetMethods()
                .First(m => m.Name == nameof(NodeRegistry.Register) && m.IsGenericMethod)
                .MakeGenericMethod(nodeType);
            registerMethod.Invoke(registry, null);
            
            var nodeInstance = (INode)Activator.CreateInstance(nodeType)!;
            
            // Act
            var createdNode = registry.CreateNode(nodeInstance.Type, default);
            var definition = registry.GetDefinition(nodeInstance.Type);
            
            // Assert
            Assert.NotNull(createdNode);
            Assert.NotNull(definition);
            Assert.Equal(createdNode.Type, definition.Type);
            Assert.Equal(createdNode.Category, definition.Category);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 4: Node Registration and Metadata Completeness
    /// For any custom node with attributes, the registry SHALL extract and provide
    /// the metadata defined in those attributes.
    /// Validates: Requirements 2.6, 2.7
    /// </summary>
    [Fact]
    public void CustomNodeWithAttributes_MetadataMatchesAttributes()
    {
        // Arrange
        var registry = new NodeRegistry();
        
        // Act
        registry.Register<FullyAttributedTestNode>();
        var definition = registry.GetDefinition("fully-attributed-test");
        
        // Assert
        Assert.NotNull(definition);
        Assert.Equal("fully-attributed-test", definition.Type);
        Assert.Equal("Fully Attributed Test", definition.Name);
        Assert.Equal("A test node with all attributes defined", definition.Description);
        Assert.Equal("test-icon", definition.Icon);
        Assert.Equal(NodeCategory.Action, definition.Category);
        
        // Verify inputs from attributes
        Assert.Equal(2, definition.Inputs.Count);
        var primaryInput = definition.Inputs.First(i => i.Name == "primary");
        Assert.Equal("Primary Input", primaryInput.DisplayName);
        Assert.Equal(PortType.Object, primaryInput.Type);
        Assert.True(primaryInput.IsRequired);
        
        var secondaryInput = definition.Inputs.First(i => i.Name == "secondary");
        Assert.Equal("Secondary Input", secondaryInput.DisplayName);
        Assert.Equal(PortType.Array, secondaryInput.Type);
        Assert.False(secondaryInput.IsRequired);
        
        // Verify outputs from attributes
        Assert.Equal(2, definition.Outputs.Count);
        var successOutput = definition.Outputs.First(o => o.Name == "success");
        Assert.Equal("Success", successOutput.DisplayName);
        Assert.Equal(PortType.Object, successOutput.Type);
        
        var errorOutput = definition.Outputs.First(o => o.Name == "error");
        Assert.Equal("Error", errorOutput.DisplayName);
        Assert.Equal(PortType.String, errorOutput.Type);
        
        // Verify configuration schema
        Assert.NotEqual(default, definition.ConfigurationSchema);
    }

    /// <summary>
    /// Feature: flowforge, Property 4: Node Registration and Metadata Completeness
    /// For any node without explicit attributes, the registry SHALL provide
    /// default metadata values.
    /// Validates: Requirements 2.6, 2.7
    /// </summary>
    [Fact]
    public void NodeWithoutAttributes_HasDefaultMetadata()
    {
        // Arrange
        var registry = new NodeRegistry();
        
        // Act
        registry.Register<MinimalTestNode>();
        var definition = registry.GetDefinition("minimal-test");
        
        // Assert
        Assert.NotNull(definition);
        Assert.Equal("minimal-test", definition.Type);
        
        // Name should be derived from type name
        Assert.False(string.IsNullOrWhiteSpace(definition.Name));
        
        // Description can be empty but not null
        Assert.NotNull(definition.Description);
        
        // Should have default icon based on category
        Assert.False(string.IsNullOrWhiteSpace(definition.Icon));
        
        // Should have default input port
        Assert.NotEmpty(definition.Inputs);
        Assert.Contains(definition.Inputs, i => i.Name == "input");
        
        // Should have default output port
        Assert.NotEmpty(definition.Outputs);
        Assert.Contains(definition.Outputs, o => o.Name == "output");
    }

    private static List<Type> GetAllTestNodeTypes()
    {
        return
        [
            // Built-in trigger nodes
            typeof(ManualTriggerNode),
            typeof(WebhookTriggerNode),
            typeof(ScheduleTriggerNode),
            // Built-in logic nodes
            typeof(IfNode),
            typeof(SwitchNode),
            typeof(LoopNode),
            typeof(MergeNode),
            // Test nodes with various configurations
            typeof(FullyAttributedTestNode),
            typeof(MinimalTestNode),
            typeof(PartialAttributeTestNode)
        ];
    }
}

#region Test Node Classes

/// <summary>
/// Test node with all attributes fully defined.
/// </summary>
[NodeDefinition(
    Name = "Fully Attributed Test",
    Description = "A test node with all attributes defined",
    Icon = "test-icon")]
[NodeInput("primary", DisplayName = "Primary Input", Type = PortType.Object, IsRequired = true)]
[NodeInput("secondary", DisplayName = "Secondary Input", Type = PortType.Array, IsRequired = false)]
[NodeOutput("success", DisplayName = "Success", Type = PortType.Object)]
[NodeOutput("error", DisplayName = "Error", Type = PortType.String)]
[ConfigurationProperty("setting1", "string", Description = "First setting", IsRequired = true)]
[ConfigurationProperty("setting2", "number", Description = "Second setting", IsRequired = false)]
internal class FullyAttributedTestNode : INode
{
    public string Id => Guid.NewGuid().ToString();
    public string Type => "fully-attributed-test";
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

/// <summary>
/// Test node with minimal attributes (no decorators).
/// </summary>
internal class MinimalTestNode : INode
{
    public string Id => Guid.NewGuid().ToString();
    public string Type => "minimal-test";
    public NodeCategory Category => NodeCategory.Transform;

    public Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        return Task.FromResult(new NodeOutput
        {
            Data = JsonDocument.Parse("{}").RootElement,
            Success = true
        });
    }
}

/// <summary>
/// Test node with partial attributes (some defined, some using defaults).
/// </summary>
[NodeDefinition(Name = "Partial Test", Description = "Partial attributes")]
[NodeOutput("result", DisplayName = "Result")]
internal class PartialAttributeTestNode : INode
{
    public string Id => Guid.NewGuid().ToString();
    public string Type => "partial-test";
    public NodeCategory Category => NodeCategory.Logic;

    public Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        return Task.FromResult(new NodeOutput
        {
            Data = JsonDocument.Parse("{}").RootElement,
            Success = true
        });
    }
}

#endregion
