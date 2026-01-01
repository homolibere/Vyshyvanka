using System.Text.Json;
using CsCheck;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using FlowForge.Designer.Models;
using FlowForge.Designer.Services;

namespace FlowForge.Tests.Property;

/// <summary>
/// Property-based tests for schema-less node fallback behavior.
/// </summary>
public class SchemaLessFallbackTests
{
    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 13: Schema-less Fallback
    /// For any node without a ConfigurationSchema (null or empty), the Configuration_Panel
    /// should display only the raw JSON editor.
    /// Validates: Requirements 9.1
    /// </summary>
    [Fact]
    public void SchemaLessNodeShowsJsonEditorOnly()
    {
        GenSchemaLessNode.Sample(testCase =>
        {
            // Arrange
            var service = new WorkflowStateService();
            service.SetNodeDefinitions([testCase.Definition]);
            
            var node = new WorkflowNode
            {
                Id = Guid.NewGuid().ToString(),
                Type = testCase.Definition.Type,
                Name = testCase.Definition.Name,
                Position = new Position(100, 100),
                Configuration = testCase.Configuration
            };
            service.AddNode(node);

            // Act: Get the node definition and check schema
            var definition = service.GetNodeDefinition(node.Type);
            
            // Assert: Definition should have no schema
            Assert.NotNull(definition);
            Assert.True(
                !definition.ConfigurationSchema.HasValue || 
                IsEmptySchema(definition.ConfigurationSchema.Value),
                "Schema-less node should have null or empty ConfigurationSchema");

            // Assert: Parsing the schema should return empty properties
            var properties = ConfigurationSchemaParser.Parse(definition.ConfigurationSchema);
            Assert.Empty(properties);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 13: Schema-less Node Configuration Preserved
    /// For any node without a ConfigurationSchema, the existing configuration should be
    /// preserved and accessible as raw JSON.
    /// Validates: Requirements 9.1
    /// </summary>
    [Fact]
    public void SchemaLessNodeConfigurationPreserved()
    {
        GenSchemaLessNodeWithConfig.Sample(testCase =>
        {
            // Arrange
            var service = new WorkflowStateService();
            service.SetNodeDefinitions([testCase.Definition]);
            
            var node = new WorkflowNode
            {
                Id = Guid.NewGuid().ToString(),
                Type = testCase.Definition.Type,
                Name = testCase.Definition.Name,
                Position = new Position(100, 100),
                Configuration = testCase.Configuration
            };
            service.AddNode(node);

            // Act: Retrieve the node
            var retrievedNode = service.GetNode(node.Id);

            // Assert: Configuration should be preserved
            Assert.NotNull(retrievedNode);
            Assert.True(
                JsonElementsEqual(testCase.Configuration, retrievedNode.Configuration),
                "Configuration should be preserved for schema-less nodes");
        }, iter: 100);
    }

    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 13: Schema-less Node Can Update Configuration
    /// For any node without a ConfigurationSchema, updating the configuration via raw JSON
    /// should persist the changes correctly.
    /// Validates: Requirements 9.1
    /// </summary>
    [Fact]
    public void SchemaLessNodeCanUpdateConfiguration()
    {
        GenSchemaLessNodeConfigUpdate.Sample(testCase =>
        {
            // Arrange
            var service = new WorkflowStateService();
            service.SetNodeDefinitions([testCase.Definition]);
            
            var node = new WorkflowNode
            {
                Id = Guid.NewGuid().ToString(),
                Type = testCase.Definition.Type,
                Name = testCase.Definition.Name,
                Position = new Position(100, 100),
                Configuration = testCase.OriginalConfig
            };
            service.AddNode(node);
            service.MarkAsSaved();

            // Act: Update configuration
            service.UpdateNodeConfiguration(node.Id, testCase.NewConfig);

            // Assert: New configuration should be persisted
            var updatedNode = service.GetNode(node.Id);
            Assert.NotNull(updatedNode);
            Assert.True(
                JsonElementsEqual(testCase.NewConfig, updatedNode.Configuration),
                "Updated configuration should be persisted for schema-less nodes");
            Assert.True(service.IsDirty, "Service should be marked dirty after update");
        }, iter: 100);
    }

    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 13: Empty Schema Treated as Schema-less
    /// For any node with an empty ConfigurationSchema (no properties), the node should be
    /// treated as schema-less and show only the JSON editor.
    /// Validates: Requirements 9.1, 9.2
    /// </summary>
    [Fact]
    public void EmptySchemaTreatedAsSchemaLess()
    {
        GenEmptySchemaNode.Sample(testCase =>
        {
            // Arrange
            var service = new WorkflowStateService();
            service.SetNodeDefinitions([testCase.Definition]);

            // Act: Parse the schema
            var properties = ConfigurationSchemaParser.Parse(testCase.Definition.ConfigurationSchema);

            // Assert: Should have no properties (treated as schema-less)
            Assert.Empty(properties);
        }, iter: 100);
    }

    #region Helpers

    private static bool IsEmptySchema(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
            return true;

        if (!schema.TryGetProperty("properties", out var properties))
            return true;

        if (properties.ValueKind != JsonValueKind.Object)
            return true;

        return !properties.EnumerateObject().Any();
    }

    private static bool JsonElementsEqual(JsonElement a, JsonElement? b)
    {
        if (!b.HasValue)
            return a.ValueKind == JsonValueKind.Undefined || a.ValueKind == JsonValueKind.Null;

        return JsonElementsEqual(a, b.Value);
    }

    private static bool JsonElementsEqual(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
            return false;

        return a.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => true,
            JsonValueKind.True or JsonValueKind.False => a.GetBoolean() == b.GetBoolean(),
            JsonValueKind.Number => Math.Abs(a.GetDouble() - b.GetDouble()) < 0.000001,
            JsonValueKind.String => a.GetString() == b.GetString(),
            JsonValueKind.Array => JsonArraysEqual(a, b),
            JsonValueKind.Object => JsonObjectsEqual(a, b),
            _ => false
        };
    }

    private static bool JsonArraysEqual(JsonElement a, JsonElement b)
    {
        var aArray = a.EnumerateArray().ToList();
        var bArray = b.EnumerateArray().ToList();

        if (aArray.Count != bArray.Count)
            return false;

        for (int i = 0; i < aArray.Count; i++)
        {
            if (!JsonElementsEqual(aArray[i], bArray[i]))
                return false;
        }

        return true;
    }

    private static bool JsonObjectsEqual(JsonElement a, JsonElement b)
    {
        var aProps = a.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        var bProps = b.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

        if (aProps.Count != bProps.Count)
            return false;

        foreach (var (key, aValue) in aProps)
        {
            if (!bProps.TryGetValue(key, out var bValue))
                return false;

            if (!JsonElementsEqual(aValue, bValue))
                return false;
        }

        return true;
    }

    #endregion

    #region Generators

    private record SchemaLessNodeTestCase(
        NodeDefinition Definition,
        JsonElement Configuration);

    private record SchemaLessNodeConfigUpdateTestCase(
        NodeDefinition Definition,
        JsonElement OriginalConfig,
        JsonElement NewConfig);

    private static NodeDefinition CreateSchemaLessDefinition(string suffix)
    {
        return new NodeDefinition
        {
            Type = $"schema-less-node-{suffix}",
            Name = $"Schema-less Node {suffix}",
            Description = "A node without configuration schema",
            Category = FlowForge.Core.Enums.NodeCategory.Action,
            ConfigurationSchema = null,
            Inputs = [new PortDefinition { Name = "input", DisplayName = "Input", Type = FlowForge.Core.Enums.PortType.Any }],
            Outputs = [new PortDefinition { Name = "output", DisplayName = "Output", Type = FlowForge.Core.Enums.PortType.Any }]
        };
    }

    private static NodeDefinition CreateEmptySchemaDefinition(string suffix)
    {
        var emptySchema = JsonSerializer.SerializeToElement(new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>()
        });

        return new NodeDefinition
        {
            Type = $"empty-schema-node-{suffix}",
            Name = $"Empty Schema Node {suffix}",
            Description = "A node with empty configuration schema",
            Category = FlowForge.Core.Enums.NodeCategory.Action,
            ConfigurationSchema = emptySchema,
            Inputs = [new PortDefinition { Name = "input", DisplayName = "Input", Type = FlowForge.Core.Enums.PortType.Any }],
            Outputs = [new PortDefinition { Name = "output", DisplayName = "Output", Type = FlowForge.Core.Enums.PortType.Any }]
        };
    }

    private static readonly Gen<string> GenSuffix =
        Gen.Char['a', 'z'].Array[3, 8].Select(chars => new string(chars));

    private static readonly Gen<JsonElement> GenArbitraryConfig =
        from keyCount in Gen.Int[0, 5]
        from keys in Gen.Char['a', 'z'].Array[3, 10].Select(chars => new string(chars)).List[keyCount, keyCount]
        from seed in Gen.Int[0, 10000]
        select BuildArbitraryConfig(keys, seed);

    private static JsonElement BuildArbitraryConfig(List<string> keys, int seed)
    {
        var random = new Random(seed);
        var config = new Dictionary<string, object?>();

        var uniqueKeys = keys.Distinct().ToList();
        foreach (var key in uniqueKeys)
        {
            // Generate random value types
            var valueType = random.Next(4);
            config[key] = valueType switch
            {
                0 => $"value_{random.Next(1000)}",
                1 => random.NextDouble() * 100,
                2 => random.Next(2) == 1,
                3 => new Dictionary<string, object> { ["nested"] = $"data_{random.Next(100)}" },
                _ => (object?)null
            };
        }

        return JsonSerializer.SerializeToElement(config);
    }

    private static readonly Gen<SchemaLessNodeTestCase> GenSchemaLessNode =
        from suffix in GenSuffix
        from config in GenArbitraryConfig
        select new SchemaLessNodeTestCase(CreateSchemaLessDefinition(suffix), config);

    private static readonly Gen<SchemaLessNodeTestCase> GenSchemaLessNodeWithConfig =
        from suffix in GenSuffix
        from config in GenArbitraryConfig
        select new SchemaLessNodeTestCase(CreateSchemaLessDefinition(suffix), config);

    private static readonly Gen<SchemaLessNodeConfigUpdateTestCase> GenSchemaLessNodeConfigUpdate =
        from suffix in GenSuffix
        from originalConfig in GenArbitraryConfig
        from newConfig in GenArbitraryConfig
        select new SchemaLessNodeConfigUpdateTestCase(
            CreateSchemaLessDefinition(suffix),
            originalConfig,
            newConfig);

    private static readonly Gen<SchemaLessNodeTestCase> GenEmptySchemaNode =
        from suffix in GenSuffix
        from config in GenArbitraryConfig
        select new SchemaLessNodeTestCase(CreateEmptySchemaDefinition(suffix), config);

    #endregion
}
