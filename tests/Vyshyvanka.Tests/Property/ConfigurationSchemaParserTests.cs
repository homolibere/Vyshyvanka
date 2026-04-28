using System.Text.Json;
using CsCheck;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;

namespace Vyshyvanka.Tests.Property;

/// <summary>
/// Property-based tests for ConfigurationSchemaParser.
/// </summary>
public class ConfigurationSchemaParserTests
{
    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 8: Configuration Round-Trip
    /// For any valid node configuration, opening the modal should populate all Property_Editors
    /// with the correct values, and serializing those values back should produce an equivalent
    /// JSON configuration.
    /// Validates: Requirements 4.1, 4.4
    /// </summary>
    [Fact]
    public void ConfigurationRoundTrip_PreservesValues()
    {
        GenSchemaAndConfig.Sample(testCase =>
        {
            var (schema, originalConfig) = testCase;

            // Act: Parse schema to get properties
            var properties = ConfigurationSchemaParser.Parse(schema);

            // Act: Extract values from original config
            var values = ConfigurationSchemaParser.ExtractValues(originalConfig, properties);

            // Act: Build configuration back from values
            var rebuiltConfig = ConfigurationSchemaParser.BuildConfiguration(values);

            // Assert: Rebuilt config should be equivalent to original
            AssertConfigurationsEquivalent(originalConfig, rebuiltConfig, properties);
        }, iter: 100);
    }

    private static void AssertConfigurationsEquivalent(
        JsonElement? original,
        JsonElement rebuilt,
        List<ConfigurationProperty> properties)
    {
        // For each property in the schema, the values should match
        foreach (var prop in properties)
        {
            var originalValue = GetPropertyValue(original, prop.Name);
            var rebuiltValue = GetPropertyValue(rebuilt, prop.Name);

            AssertValuesEquivalent(originalValue, rebuiltValue, prop.Name, prop.Type);
        }
    }

    private static JsonElement? GetPropertyValue(JsonElement? config, string propertyName)
    {
        if (!config.HasValue || config.Value.ValueKind != JsonValueKind.Object)
            return null;

        if (config.Value.TryGetProperty(propertyName, out var value))
            return value;

        return null;
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

    private static void AssertValuesEquivalent(
        JsonElement? original,
        JsonElement? rebuilt,
        string propertyName,
        string propertyType)
    {
        // Both null is equivalent
        if (!original.HasValue && !rebuilt.HasValue)
            return;

        // If original is null/undefined, rebuilt should also be absent
        if (!original.HasValue || original.Value.ValueKind == JsonValueKind.Null ||
            original.Value.ValueKind == JsonValueKind.Undefined)
        {
            Assert.True(
                !rebuilt.HasValue ||
                rebuilt.Value.ValueKind == JsonValueKind.Null ||
                rebuilt.Value.ValueKind == JsonValueKind.Undefined,
                $"Property '{propertyName}': expected null/absent but got {rebuilt?.ValueKind}");
            return;
        }

        Assert.True(rebuilt.HasValue, $"Property '{propertyName}': expected value but got null");

        // Compare based on type
        switch (propertyType)
        {
            case "string":
                Assert.Equal(original.Value.GetString(), rebuilt.Value.GetString());
                break;

            case "number":
            case "integer":
                Assert.Equal(original.Value.GetDouble(), rebuilt.Value.GetDouble(), 6);
                break;

            case "boolean":
                Assert.Equal(original.Value.GetBoolean(), rebuilt.Value.GetBoolean());
                break;

            case "object":
            case "array":
                // Compare as parsed JSON (ignore formatting differences)
                var originalJson = JsonSerializer.Deserialize<JsonElement>(original.Value.GetRawText());
                var rebuiltJson = JsonSerializer.Deserialize<JsonElement>(rebuilt.Value.GetRawText());
                Assert.True(
                    JsonElementsEqual(originalJson, rebuiltJson),
                    $"Property '{propertyName}': JSON values differ. Expected: {original.Value.GetRawText()}, Actual: {rebuilt.Value.GetRawText()}");
                break;

            default:
                // Default to string comparison
                Assert.Equal(original.Value.GetRawText(), rebuilt.Value.GetRawText());
                break;
        }
    }

    #region Generators

    /// <summary>Generator for property types.</summary>
    private static readonly Gen<string> GenPropertyType =
        Gen.OneOf(
            Gen.Const("string"),
            Gen.Const("number"),
            Gen.Const("boolean"),
            Gen.Const("object"),
            Gen.Const("array")
        );

    /// <summary>Generator for non-empty alphanumeric property names.</summary>
    private static readonly Gen<string> GenPropertyName =
        Gen.Char['a', 'z'].Array[3, 15].Select(chars => new string(chars));

    /// <summary>Generator for optional description.</summary>
    private static readonly Gen<string?> GenDescription =
        Gen.Bool.SelectMany(hasDesc =>
            hasDesc
                ? Gen.Char['a', 'z'].Array[5, 50].Select(chars => (string?)new string(chars))
                : Gen.Const((string?)null));

    /// <summary>Generator for a schema property definition.</summary>
    private static readonly Gen<(string Name, string Type, string? Description, bool IsRequired)> GenSchemaProperty =
        from name in GenPropertyName
        from type in GenPropertyType
        from description in GenDescription
        from isRequired in Gen.Bool
        select (name, type, description, isRequired);

    /// <summary>Generator for a value matching a property type.</summary>
    private static Gen<object?> GenValueForType(string type) =>
        type switch
        {
            "string" => Gen.Char['a', 'z'].Array[1, 20].Select(chars => (object?)new string(chars)),
            "number" => Gen.Double[-1000, 1000].Select(d => (object?)d),
            "boolean" => Gen.Bool.Select(b => (object?)b),
            "object" => Gen.Const((object?)new Dictionary<string, object> { ["key"] = "value" }),
            "array" => Gen.Const((object?)new List<object> { "item1", "item2" }),
            _ => Gen.Const((object?)null)
        };

    /// <summary>Generator for a schema with properties and matching configuration.</summary>
    private static readonly Gen<(JsonElement? Schema, JsonElement? Config)> GenSchemaAndConfig =
        from propertyCount in Gen.Int[1, 5]
        from properties in GenSchemaProperty.List[propertyCount, propertyCount]
        from includeNulls in Gen.Bool
        select BuildSchemaAndConfig(properties, includeNulls);

    private static (JsonElement? Schema, JsonElement? Config) BuildSchemaAndConfig(
        List<(string Name, string Type, string? Description, bool IsRequired)> properties,
        bool includeNulls)
    {
        // Ensure unique property names
        var uniqueProperties = properties
            .GroupBy(p => p.Name)
            .Select(g => g.First())
            .ToList();

        // Build schema
        var schemaProperties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var (name, type, description, isRequired) in uniqueProperties)
        {
            var propDef = new Dictionary<string, object>
            {
                ["type"] = type
            };
            if (description != null)
                propDef["description"] = description;

            schemaProperties[name] = propDef;

            if (isRequired)
                required.Add(name);
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = schemaProperties,
            ["required"] = required
        };

        var schemaElement = JsonSerializer.SerializeToElement(schema);

        // Build config with values for each property
        var config = new Dictionary<string, object?>();
        var random = new Random(42); // Fixed seed for reproducibility

        foreach (var (name, type, _, _) in uniqueProperties)
        {
            // Sometimes include null values
            if (includeNulls && random.Next(3) == 0)
            {
                // Skip this property (null)
                continue;
            }

            config[name] = type switch
            {
                "string" => $"value_{name}",
                "number" => random.NextDouble() * 100,
                "boolean" => random.Next(2) == 1,
                "object" => new Dictionary<string, object> { ["nested"] = "data" },
                "array" => new List<object> { "item1", 42 },
                _ => null
            };
        }

        var configElement = JsonSerializer.SerializeToElement(config);

        return (schemaElement, configElement);
    }

    #endregion
}
