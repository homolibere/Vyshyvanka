using System.Text.Json;
using CsCheck;
using FlowForge.Designer.Models;
using FlowForge.Designer.Services;

namespace FlowForge.Tests.Property;

/// <summary>
/// Property-based tests for form/JSON mode round-trip in NodeEditorConfigPanel.
/// </summary>
public class FormJsonModeRoundTripTests
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 12: Form/JSON Mode Round-Trip
    /// For any valid form configuration, switching to JSON mode should produce valid JSON,
    /// and switching back to form mode should restore the same values.
    /// For invalid JSON, switching to form mode should fail gracefully and remain in JSON mode.
    /// Validates: Requirements 8.2, 8.3, 8.4
    /// </summary>
    [Fact]
    public void FormJsonModeRoundTrip_PreservesValues()
    {
        GenPropertiesWithValues.Sample(testCase =>
        {
            var (properties, originalValues) = testCase;

            // Act: Serialize form values to JSON (switching to JSON mode)
            var json = SerializeValuesToJson(originalValues);

            // Act: Parse JSON back to values (switching to form mode)
            var success = TryParseJsonToValues(json, properties, out var restoredValues, out _);

            // Assert: Round-trip should succeed and preserve values
            Assert.True(success, "JSON parsing should succeed for valid form values");
            AssertValuesEquivalent(originalValues, restoredValues!, properties);
        }, iter: 100);
    }

    /// <summary>
    /// For any valid JSON configuration, parsing should succeed and produce correct values.
    /// </summary>
    [Fact]
    public void ValidJson_ParsesSuccessfully()
    {
        GenValidJsonConfig.Sample(testCase =>
        {
            var (properties, json) = testCase;

            // Act
            var success = TryParseJsonToValues(json, properties, out var values, out var error);

            // Assert
            Assert.True(success, $"Valid JSON should parse successfully. Error: {error}");
            Assert.NotNull(values);
            Assert.Null(error);
        }, iter: 100);
    }

    /// <summary>
    /// For any invalid JSON string, parsing should fail gracefully with an error message.
    /// </summary>
    [Fact]
    public void InvalidJson_FailsGracefully()
    {
        GenInvalidJson.Sample(invalidJson =>
        {
            var properties = new List<ConfigurationProperty>
            {
                new() { Name = "test", DisplayName = "Test", Type = "string" }
            };

            // Act
            var success = TryParseJsonToValues(invalidJson, properties, out var values, out var error);

            // Assert
            Assert.False(success, "Invalid JSON should fail to parse");
            Assert.NotNull(error);
            Assert.Contains("Invalid JSON", error);
        }, iter: 100);
    }

    /// <summary>
    /// For any non-object JSON (array, string, number), parsing should fail with appropriate error.
    /// </summary>
    [Fact]
    public void NonObjectJson_FailsWithError()
    {
        GenNonObjectJson.Sample(json =>
        {
            var properties = new List<ConfigurationProperty>
            {
                new() { Name = "test", DisplayName = "Test", Type = "string" }
            };

            // Act
            var success = TryParseJsonToValues(json, properties, out _, out var error);

            // Assert
            Assert.False(success, "Non-object JSON should fail to parse");
            Assert.NotNull(error);
            Assert.Contains("must be a JSON object", error);
        }, iter: 100);
    }

    /// <summary>
    /// Empty or whitespace JSON should be handled gracefully.
    /// </summary>
    [Fact]
    public void EmptyJson_HandledGracefully()
    {
        GenEmptyJson.Sample(json =>
        {
            var properties = new List<ConfigurationProperty>
            {
                new() { Name = "test", DisplayName = "Test", Type = "string", IsRequired = false }
            };

            // Act
            var success = TryParseJsonToValues(json, properties, out var values, out _);

            // Assert: Empty JSON should result in null values for all properties
            Assert.True(success, "Empty JSON should be handled gracefully");
            Assert.NotNull(values);
            foreach (var prop in properties)
            {
                Assert.True(values.ContainsKey(prop.Name));
                Assert.Null(values[prop.Name]);
            }
        }, iter: 100);
    }

    /// <summary>
    /// Multiple round-trips should preserve values consistently.
    /// </summary>
    [Fact]
    public void MultipleRoundTrips_PreserveValues()
    {
        GenPropertiesWithValues.Sample(testCase =>
        {
            var (properties, originalValues) = testCase;

            // Act: Multiple round-trips
            var currentValues = originalValues;
            for (int i = 0; i < 3; i++)
            {
                var json = SerializeValuesToJson(currentValues);
                var success = TryParseJsonToValues(json, properties, out var newValues, out _);
                Assert.True(success, $"Round-trip {i + 1} should succeed");
                currentValues = newValues!;
            }

            // Assert: Final values should match original
            AssertValuesEquivalent(originalValues, currentValues, properties);
        }, iter: 100);
    }

    #region Mode Switching Logic (mirrors NodeEditorConfigPanel)

    private static string SerializeValuesToJson(Dictionary<string, object?> values)
    {
        try
        {
            var config = ConfigurationSchemaParser.BuildConfiguration(values);
            return JsonSerializer.Serialize(config, IndentedOptions);
        }
        catch
        {
            return "{}";
        }
    }

    private static bool TryParseJsonToValues(
        string json,
        List<ConfigurationProperty> properties,
        out Dictionary<string, object?>? values,
        out string? error)
    {
        values = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            values = new Dictionary<string, object?>();
            foreach (var prop in properties)
            {
                values[prop.Name] = null;
            }
            return true;
        }

        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "Configuration must be a JSON object";
                return false;
            }

            values = ConfigurationSchemaParser.ExtractValues(doc.RootElement, properties);
            return true;
        }
        catch (JsonException ex)
        {
            error = $"Invalid JSON: {ex.Message}";
            return false;
        }
    }

    private static void AssertValuesEquivalent(
        Dictionary<string, object?> original,
        Dictionary<string, object?> restored,
        List<ConfigurationProperty> properties)
    {
        foreach (var prop in properties)
        {
            var originalValue = original.GetValueOrDefault(prop.Name);
            var restoredValue = restored.GetValueOrDefault(prop.Name);

            AssertValueEquivalent(originalValue, restoredValue, prop.Name, prop.Type);
        }
    }

    private static void AssertValueEquivalent(object? original, object? restored, string propName, string propType)
    {
        // Both null is equivalent
        if (original is null && restored is null)
            return;

        // Handle JsonElement comparisons
        if (original is JsonElement origElement && restored is JsonElement restElement)
        {
            Assert.True(
                JsonElementsEqual(origElement, restElement),
                $"Property '{propName}': JSON elements differ");
            return;
        }

        // Handle primitive comparisons
        switch (propType)
        {
            case "string":
                Assert.Equal(original?.ToString(), restored?.ToString());
                break;

            case "number":
            case "integer":
                var origNum = Convert.ToDouble(original);
                var restNum = Convert.ToDouble(restored);
                Assert.Equal(origNum, restNum, 6);
                break;

            case "boolean":
                Assert.Equal(Convert.ToBoolean(original), Convert.ToBoolean(restored));
                break;

            case "object":
            case "array":
                // Both should be JsonElements at this point
                if (original is JsonElement oe && restored is JsonElement re)
                {
                    Assert.True(JsonElementsEqual(oe, re), $"Property '{propName}': JSON values differ");
                }
                break;

            default:
                Assert.Equal(original, restored);
                break;
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

    #endregion

    #region Generators

    private static readonly Gen<string> GenPropertyName =
        Gen.Char['a', 'z'].Array[3, 15].Select(chars => new string(chars));

    private static readonly Gen<string> GenDisplayName =
        Gen.Char['A', 'Z'].SelectMany(first =>
            Gen.Char['a', 'z'].Array[2, 14].Select(rest =>
                first + new string(rest)));

    private static readonly Gen<string> GenPropertyType =
        Gen.OneOf(
            Gen.Const("string"),
            Gen.Const("number"),
            Gen.Const("boolean"),
            Gen.Const("object"),
            Gen.Const("array")
        );

    private static readonly Gen<ConfigurationProperty> GenConfigurationProperty =
        from name in GenPropertyName
        from displayName in GenDisplayName
        from type in GenPropertyType
        from isRequired in Gen.Bool
        select new ConfigurationProperty
        {
            Name = name,
            DisplayName = displayName,
            Type = type,
            IsRequired = isRequired,
            Options = null
        };

    private static object? GenerateValueForType(string type) =>
        type switch
        {
            "string" => "test_value_" + Guid.NewGuid().ToString()[..8],
            "number" => 42.5,
            "boolean" => true,
            "object" => JsonSerializer.SerializeToElement(new { key = "value", nested = new { inner = 123 } }),
            "array" => JsonSerializer.SerializeToElement(new object[] { "item1", 42, true }),
            _ => "default"
        };

    private static readonly Gen<(List<ConfigurationProperty> Properties, Dictionary<string, object?> Values)> GenPropertiesWithValues =
        from propertyCount in Gen.Int[1, 5]
        from properties in GenConfigurationProperty.List[propertyCount, propertyCount]
        select BuildPropertiesWithValues(properties);

    private static (List<ConfigurationProperty> Properties, Dictionary<string, object?> Values) BuildPropertiesWithValues(
        List<ConfigurationProperty> properties)
    {
        var uniqueProperties = properties
            .GroupBy(p => p.Name)
            .Select(g => g.First())
            .ToList();

        var values = new Dictionary<string, object?>();
        var random = new Random(42);

        foreach (var prop in uniqueProperties)
        {
            // Sometimes include null values for optional properties
            var makeNull = !prop.IsRequired && random.Next(3) == 0;
            values[prop.Name] = makeNull ? null : GenerateValueForType(prop.Type);
        }

        return (uniqueProperties, values);
    }

    private static readonly Gen<(List<ConfigurationProperty> Properties, string Json)> GenValidJsonConfig =
        from properties in GenPropertiesWithValues
        select (properties.Properties, SerializeValuesToJson(properties.Values));

    private static readonly Gen<string> GenInvalidJson =
        Gen.OneOf(
            Gen.Const("{invalid json}"),
            Gen.Const("{ \"key\": }"),
            Gen.Const("{ \"key\" \"value\" }"),
            Gen.Const("{ key: \"value\" }"),
            Gen.Const("{ \"key\": undefined }"),
            Gen.Const("{ \"key\": 'single quotes' }")
        );

    private static readonly Gen<string> GenNonObjectJson =
        Gen.OneOf(
            Gen.Const("[1, 2, 3]"),
            Gen.Const("\"just a string\""),
            Gen.Const("42"),
            Gen.Const("true"),
            Gen.Const("null")
        );

    private static readonly Gen<string> GenEmptyJson =
        Gen.OneOf(
            Gen.Const(""),
            Gen.Const("   "),
            Gen.Const("\t\n")
        );

    #endregion
}
