using System.Text.Json;
using CsCheck;
using FlowForge.Designer.Services;

namespace FlowForge.Tests.Property;

/// <summary>
/// Property-based tests for verifying property count matches schema.
/// </summary>
public class PropertyCountMatchesSchemaTests
{
    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 4: Property Count Matches Schema
    /// For any node with a ConfigurationSchema containing N properties, the Configuration_Panel
    /// should render exactly N Property_Editor components.
    /// Validates: Requirements 3.1
    /// </summary>
    [Fact]
    public void PropertyCountMatchesSchema_ParsedPropertiesMatchSchemaPropertyCount()
    {
        // Generator for property count
        var genPropertyCount = Gen.Int[0, 10];

        genPropertyCount.Sample(expectedCount =>
        {
            // Build a schema with the expected number of properties
            var schema = BuildSchemaWithPropertyCount(expectedCount);

            // Act: Parse the schema to get properties
            var properties = ConfigurationSchemaParser.Parse(schema);

            // Assert: The number of parsed properties should match the expected count
            Assert.Equal(expectedCount, properties.Count);
        }, iter: 100);
    }

    /// <summary>
    /// For any schema with unique property names, each property should be parsed exactly once.
    /// </summary>
    [Fact]
    public void PropertyCountMatchesSchema_EachPropertyParsedExactlyOnce()
    {
        var genPropertyCount = Gen.Int[1, 8];

        genPropertyCount.Sample(propertyCount =>
        {
            // Generate unique property names
            var propertyNames = Enumerable.Range(0, propertyCount)
                .Select(i => $"property_{i}")
                .ToList();

            // Build schema with these properties
            var schema = BuildSchemaWithPropertyNames(propertyNames);

            // Act: Parse the schema
            var properties = ConfigurationSchemaParser.Parse(schema);

            // Assert: Each expected property name should appear exactly once
            var parsedNames = properties.Select(p => p.Name).ToList();

            Assert.Equal(propertyNames.Count, parsedNames.Count);

            foreach (var expectedName in propertyNames)
            {
                Assert.Contains(expectedName, parsedNames);
            }

            // Verify no duplicates
            Assert.Equal(parsedNames.Count, parsedNames.Distinct().Count());
        }, iter: 100);
    }

    /// <summary>
    /// For any empty schema (no properties), the parser should return zero properties.
    /// </summary>
    [Fact]
    public void PropertyCountMatchesSchema_EmptySchemaReturnsZeroProperties()
    {
        // Test with empty properties object
        var emptyPropertiesSchema = BuildEmptyPropertiesSchema();
        var properties1 = ConfigurationSchemaParser.Parse(emptyPropertiesSchema);
        Assert.Empty(properties1);

        // Test with no properties key
        var noPropertiesKeySchema = BuildNoPropertiesKeySchema();
        var properties2 = ConfigurationSchemaParser.Parse(noPropertiesKeySchema);
        Assert.Empty(properties2);
    }

    /// <summary>
    /// For any null schema, the parser should return zero properties.
    /// </summary>
    [Fact]
    public void PropertyCountMatchesSchema_NullSchemaReturnsZeroProperties()
    {
        // Act: Parse null schema
        var properties = ConfigurationSchemaParser.Parse(null);

        // Assert: Should return empty list
        Assert.Empty(properties);
    }

    /// <summary>
    /// For any schema with various property types, all properties should be parsed.
    /// </summary>
    [Fact]
    public void PropertyCountMatchesSchema_AllPropertyTypesParsed()
    {
        var allTypes = new[] { "string", "number", "boolean", "object", "array" };
        var genTypeCount = Gen.Int[2, 5];

        genTypeCount.Sample(typeCount =>
        {
            // Select random types
            var selectedTypes = allTypes.Take(typeCount).ToList();

            // Build schema with properties of these types
            var schema = BuildSchemaWithTypes(selectedTypes);

            // Act: Parse the schema
            var properties = ConfigurationSchemaParser.Parse(schema);

            // Assert: Count matches
            Assert.Equal(selectedTypes.Count, properties.Count);

            // Assert: All expected types are represented
            var parsedTypes = properties.Select(p => p.Type).ToHashSet();
            foreach (var expectedType in selectedTypes)
            {
                Assert.Contains(expectedType, parsedTypes);
            }
        }, iter: 100);
    }

    /// <summary>
    /// For any schema with required properties, the IsRequired flag should be set correctly.
    /// </summary>
    [Fact]
    public void PropertyCountMatchesSchema_RequiredFlagsSetCorrectly()
    {
        var genPropertyCount = Gen.Int[2, 6];

        genPropertyCount.Sample(propertyCount =>
        {
            // Generate properties with some marked as required
            var propertyNames = Enumerable.Range(0, propertyCount)
                .Select(i => $"prop_{i}")
                .ToList();

            // Mark every other property as required
            var requiredNames = propertyNames.Where((_, i) => i % 2 == 0).ToList();

            var schema = BuildSchemaWithRequiredProperties(propertyNames, requiredNames);

            // Act: Parse the schema
            var properties = ConfigurationSchemaParser.Parse(schema);

            // Assert: Required flags are set correctly
            foreach (var prop in properties)
            {
                var shouldBeRequired = requiredNames.Contains(prop.Name);
                Assert.Equal(shouldBeRequired, prop.IsRequired);
            }
        }, iter: 100);
    }

    #region Schema Builders

    private static JsonElement? BuildSchemaWithPropertyCount(int count)
    {
        var schemaProperties = new Dictionary<string, object>();

        for (int i = 0; i < count; i++)
        {
            schemaProperties[$"property_{i}"] = new Dictionary<string, object>
            {
                ["type"] = "string"
            };
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = schemaProperties,
            ["required"] = new List<string>()
        };

        return JsonSerializer.SerializeToElement(schema);
    }

    private static JsonElement? BuildSchemaWithPropertyNames(List<string> propertyNames)
    {
        var schemaProperties = new Dictionary<string, object>();

        foreach (var name in propertyNames)
        {
            schemaProperties[name] = new Dictionary<string, object>
            {
                ["type"] = "string"
            };
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = schemaProperties,
            ["required"] = new List<string>()
        };

        return JsonSerializer.SerializeToElement(schema);
    }

    private static JsonElement? BuildSchemaWithTypes(List<string> types)
    {
        var schemaProperties = new Dictionary<string, object>();

        for (int i = 0; i < types.Count; i++)
        {
            schemaProperties[$"prop_{types[i]}_{i}"] = new Dictionary<string, object>
            {
                ["type"] = types[i]
            };
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = schemaProperties,
            ["required"] = new List<string>()
        };

        return JsonSerializer.SerializeToElement(schema);
    }

    private static JsonElement? BuildSchemaWithRequiredProperties(List<string> propertyNames, List<string> requiredNames)
    {
        var schemaProperties = new Dictionary<string, object>();

        foreach (var name in propertyNames)
        {
            schemaProperties[name] = new Dictionary<string, object>
            {
                ["type"] = "string"
            };
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = schemaProperties,
            ["required"] = requiredNames
        };

        return JsonSerializer.SerializeToElement(schema);
    }

    private static JsonElement? BuildEmptyPropertiesSchema()
    {
        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>(),
            ["required"] = new List<string>()
        };
        return JsonSerializer.SerializeToElement(schema);
    }

    private static JsonElement? BuildNoPropertiesKeySchema()
    {
        var schema = new Dictionary<string, object>
        {
            ["type"] = "object"
        };
        return JsonSerializer.SerializeToElement(schema);
    }

    #endregion
}
