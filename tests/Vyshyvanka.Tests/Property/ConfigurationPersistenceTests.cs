using System.Text.Json;
using CsCheck;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;

namespace Vyshyvanka.Tests.Property;

/// <summary>
/// Property-based tests for configuration persistence in the node editor modal.
/// </summary>
public class ConfigurationPersistenceTests
{
    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 2: Configuration Persists on Modal Close
    /// For any set of configuration changes made in the modal, when the modal is closed,
    /// those changes should be persisted to the WorkflowStateService and reflected in the node's configuration.
    /// Validates: Requirements 1.5, 4.3
    /// </summary>
    [Fact]
    public void ConfigurationPersistsOnModalClose()
    {
        GenConfigurationChange.Sample(testCase =>
        {
            // Arrange
            var service = new WorkflowStateService();
            service.SetNodeDefinitions([testCase.NodeDefinition]);
            
            var node = new WorkflowNode
            {
                Id = Guid.NewGuid().ToString(),
                Type = testCase.NodeDefinition.Type,
                Name = testCase.NodeDefinition.Name,
                Position = new Position(100, 100),
                Configuration = testCase.OriginalConfig
            };
            service.AddNode(node);
            service.MarkAsSaved();

            // Act: Simulate modal updating configuration
            service.UpdateNodeConfiguration(node.Id, testCase.NewConfig);

            // Assert: Configuration should be persisted
            var updatedNode = service.GetNode(node.Id);
            Assert.NotNull(updatedNode);
            
            // Verify the configuration was updated
            AssertConfigurationsEquivalent(testCase.NewConfig, updatedNode.Configuration);
            
            // Verify dirty flag is set
            Assert.True(service.IsDirty);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 2: Configuration Changes Trigger State Change Event
    /// For any configuration update, the WorkflowStateService should raise the OnStateChanged event.
    /// Validates: Requirements 1.5, 4.3
    /// </summary>
    [Fact]
    public void ConfigurationChangesTriggersStateChangeEvent()
    {
        GenConfigurationChange.Sample(testCase =>
        {
            // Arrange
            var service = new WorkflowStateService();
            service.SetNodeDefinitions([testCase.NodeDefinition]);
            
            var node = new WorkflowNode
            {
                Id = Guid.NewGuid().ToString(),
                Type = testCase.NodeDefinition.Type,
                Name = testCase.NodeDefinition.Name,
                Position = new Position(100, 100),
                Configuration = testCase.OriginalConfig
            };
            service.AddNode(node);
            
            var stateChangedCount = 0;
            service.OnStateChanged += () => stateChangedCount++;
            var initialCount = stateChangedCount;

            // Act
            service.UpdateNodeConfiguration(node.Id, testCase.NewConfig);

            // Assert: State change event should have been raised
            Assert.True(stateChangedCount > initialCount);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 2: Configuration Round-Trip Through Service
    /// For any valid configuration values, updating a node's configuration and then retrieving it
    /// should return equivalent values.
    /// Validates: Requirements 1.5, 4.3
    /// </summary>
    [Fact]
    public void ConfigurationRoundTripThroughService()
    {
        GenSchemaAndValues.Sample(testCase =>
        {
            // Arrange
            var service = new WorkflowStateService();
            var definition = CreateNodeDefinitionWithSchema(testCase.Schema);
            service.SetNodeDefinitions([definition]);
            
            var node = new WorkflowNode
            {
                Id = Guid.NewGuid().ToString(),
                Type = definition.Type,
                Name = definition.Name,
                Position = new Position(100, 100)
            };
            service.AddNode(node);

            // Act: Build configuration from values and update
            var config = ConfigurationSchemaParser.BuildConfiguration(testCase.Values);
            service.UpdateNodeConfiguration(node.Id, config);

            // Assert: Retrieve and verify
            var updatedNode = service.GetNode(node.Id);
            Assert.NotNull(updatedNode);
            
            // Extract values from the stored configuration
            var properties = ConfigurationSchemaParser.Parse(testCase.Schema);
            var retrievedValues = ConfigurationSchemaParser.ExtractValues(updatedNode.Configuration, properties);

            // Verify each value matches
            foreach (var prop in properties)
            {
                var originalValue = testCase.Values.GetValueOrDefault(prop.Name);
                var retrievedValue = retrievedValues.GetValueOrDefault(prop.Name);
                
                AssertValuesEquivalent(originalValue, retrievedValue, prop.Name, prop.Type);
            }
        }, iter: 100);
    }

    #region Assertion Helpers

    private static void AssertConfigurationsEquivalent(JsonElement expected, JsonElement? actual)
    {
        Assert.True(actual.HasValue, "Actual configuration should not be null");
        
        if (expected.ValueKind == JsonValueKind.Object && actual.Value.ValueKind == JsonValueKind.Object)
        {
            var expectedProps = expected.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
            var actualProps = actual.Value.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

            foreach (var (key, expectedValue) in expectedProps)
            {
                Assert.True(actualProps.ContainsKey(key), $"Missing property: {key}");
                Assert.True(
                    JsonElementsEqual(expectedValue, actualProps[key]),
                    $"Property '{key}' values differ");
            }
        }
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

    private static void AssertValuesEquivalent(object? original, object? retrieved, string propertyName, string propertyType)
    {
        // Both null is equivalent
        if (original is null && retrieved is null)
            return;

        // Handle JsonElement comparisons
        if (original is JsonElement origElement && retrieved is JsonElement retElement)
        {
            Assert.True(
                JsonElementsEqual(origElement, retElement),
                $"Property '{propertyName}' values differ");
            return;
        }

        // Handle primitive comparisons
        switch (propertyType)
        {
            case "string":
                Assert.Equal(original?.ToString(), retrieved?.ToString());
                break;

            case "number":
            case "integer":
                var origNum = Convert.ToDouble(original);
                var retNum = Convert.ToDouble(retrieved);
                Assert.Equal(origNum, retNum, 6);
                break;

            case "boolean":
                Assert.Equal(Convert.ToBoolean(original), Convert.ToBoolean(retrieved));
                break;

            default:
                // For complex types, compare JSON representations
                var origJson = JsonSerializer.Serialize(original);
                var retJson = JsonSerializer.Serialize(retrieved);
                Assert.Equal(origJson, retJson);
                break;
        }
    }

    #endregion

    #region Generators

    private static NodeDefinition CreateNodeDefinitionWithSchema(JsonElement schema)
    {
        return new NodeDefinition
        {
            Type = "test-node",
            Name = "Test Node",
            Description = "A test node for property testing",
            Category = Vyshyvanka.Core.Enums.NodeCategory.Action,
            ConfigurationSchema = schema,
            Inputs = [new PortDefinition { Name = "input", DisplayName = "Input", Type = Vyshyvanka.Core.Enums.PortType.Any }],
            Outputs = [new PortDefinition { Name = "output", DisplayName = "Output", Type = Vyshyvanka.Core.Enums.PortType.Any }]
        };
    }

    private static readonly Gen<string> GenPropertyName =
        Gen.Char['a', 'z'].Array[3, 10].Select(chars => new string(chars));

    private static readonly Gen<string> GenPropertyType =
        Gen.OneOf(
            Gen.Const("string"),
            Gen.Const("number"),
            Gen.Const("boolean")
        );

    private static Gen<object?> GenValueForType(string type) =>
        type switch
        {
            "string" => Gen.Char['a', 'z'].Array[1, 15].Select(chars => (object?)new string(chars)),
            "number" => Gen.Double[-100, 100].Select(d => (object?)d),
            "boolean" => Gen.Bool.Select(b => (object?)b),
            _ => Gen.Const((object?)null)
        };

    private record ConfigurationChangeTestCase(
        NodeDefinition NodeDefinition,
        JsonElement OriginalConfig,
        JsonElement NewConfig);

    private static readonly Gen<ConfigurationChangeTestCase> GenConfigurationChange =
        from propertyCount in Gen.Int[1, 4]
        from properties in (
            from name in GenPropertyName
            from type in GenPropertyType
            select (name, type)
        ).List[propertyCount, propertyCount]
        from seed in Gen.Int[0, 10000]
        select BuildConfigurationChangeTestCase(properties, seed);

    private static ConfigurationChangeTestCase BuildConfigurationChangeTestCase(
        List<(string name, string type)> properties,
        int seed)
    {
        var random = new Random(seed);
        
        // Ensure unique property names
        var uniqueProperties = properties
            .GroupBy(p => p.name)
            .Select(g => g.First())
            .ToList();

        // Build schema
        var schemaProperties = new Dictionary<string, object>();
        foreach (var (name, type) in uniqueProperties)
        {
            schemaProperties[name] = new Dictionary<string, object> { ["type"] = type };
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = schemaProperties
        };
        var schemaElement = JsonSerializer.SerializeToElement(schema);

        // Build original config
        var originalConfig = new Dictionary<string, object?>();
        foreach (var (name, type) in uniqueProperties)
        {
            originalConfig[name] = GenerateValue(type, random);
        }
        var originalElement = JsonSerializer.SerializeToElement(originalConfig);

        // Build new config (different values)
        var newConfig = new Dictionary<string, object?>();
        foreach (var (name, type) in uniqueProperties)
        {
            newConfig[name] = GenerateValue(type, random);
        }
        var newElement = JsonSerializer.SerializeToElement(newConfig);

        var definition = new NodeDefinition
        {
            Type = "test-node",
            Name = "Test Node",
            Description = "Test node",
            Category = Vyshyvanka.Core.Enums.NodeCategory.Action,
            ConfigurationSchema = schemaElement,
            Inputs = [new PortDefinition { Name = "input", DisplayName = "Input", Type = Vyshyvanka.Core.Enums.PortType.Any }],
            Outputs = [new PortDefinition { Name = "output", DisplayName = "Output", Type = Vyshyvanka.Core.Enums.PortType.Any }]
        };

        return new ConfigurationChangeTestCase(definition, originalElement, newElement);
    }

    private static object? GenerateValue(string type, Random random) =>
        type switch
        {
            "string" => $"value_{random.Next(1000)}",
            "number" => random.NextDouble() * 100,
            "boolean" => random.Next(2) == 1,
            _ => (object?)null
        };

    private record SchemaAndValuesTestCase(
        JsonElement Schema,
        Dictionary<string, object?> Values);

    private static readonly Gen<SchemaAndValuesTestCase> GenSchemaAndValues =
        from propertyCount in Gen.Int[1, 4]
        from properties in (
            from name in GenPropertyName
            from type in GenPropertyType
            select (name, type)
        ).List[propertyCount, propertyCount]
        from seed in Gen.Int[0, 10000]
        select BuildSchemaAndValuesTestCase(properties, seed);

    private static SchemaAndValuesTestCase BuildSchemaAndValuesTestCase(
        List<(string name, string type)> properties,
        int seed)
    {
        var random = new Random(seed);
        
        // Ensure unique property names
        var uniqueProperties = properties
            .GroupBy(p => p.name)
            .Select(g => g.First())
            .ToList();

        // Build schema
        var schemaProperties = new Dictionary<string, object>();
        foreach (var (name, type) in uniqueProperties)
        {
            schemaProperties[name] = new Dictionary<string, object> { ["type"] = type };
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = schemaProperties
        };
        var schemaElement = JsonSerializer.SerializeToElement(schema);

        // Build values
        var values = new Dictionary<string, object?>();
        foreach (var (name, type) in uniqueProperties)
        {
            values[name] = GenerateValue(type, random);
        }

        return new SchemaAndValuesTestCase(schemaElement, values);
    }

    #endregion
}
