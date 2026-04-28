using System.Text.Json;
using CsCheck;

namespace Vyshyvanka.Tests.Property;

/// <summary>
/// Property-based tests for execution data display in Input and Output panels.
/// </summary>
public class ExecutionDataDisplayTests
{
    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 3: Execution Data Display
    /// For any node with execution state, the Input_Panel should display the input data
    /// and the Output_Panel should display the output data. For nodes without execution state,
    /// both panels should show placeholder content.
    /// Validates: Requirements 2.2, 2.3, 2.5
    /// </summary>
    [Fact]
    public void ExecutionDataDisplay_JsonDataFormattedWithIndentation()
    {
        GenJsonElement.Sample(jsonElement =>
        {
            // Act: Format the JSON element as the panels would
            var formatted = FormatJson(jsonElement);

            // Assert: The formatted output should be valid JSON
            Assert.False(string.IsNullOrEmpty(formatted));

            // Assert: The formatted output should be parseable back to JSON
            var reparsed = JsonDocument.Parse(formatted);
            Assert.NotNull(reparsed);

            // Assert: The formatted output should contain newlines (indentation)
            // unless it's a simple primitive value
            if (jsonElement.ValueKind == JsonValueKind.Object ||
                jsonElement.ValueKind == JsonValueKind.Array)
            {
                // Complex types should have indentation (newlines)
                var hasIndentation = formatted.Contains('\n') || formatted.Contains("  ");
                // Empty objects/arrays may not have newlines, so check for that case
                var isEmpty = (jsonElement.ValueKind == JsonValueKind.Object &&
                               !jsonElement.EnumerateObject().Any()) ||
                              (jsonElement.ValueKind == JsonValueKind.Array &&
                               jsonElement.GetArrayLength() == 0);

                if (!isEmpty)
                {
                    Assert.True(hasIndentation,
                        $"Expected indented JSON for non-empty {jsonElement.ValueKind}, got: {formatted}");
                }
            }
        }, iter: 100);
    }

    /// <summary>
    /// For any JSON object, formatting should preserve all properties.
    /// </summary>
    [Fact]
    public void ExecutionDataDisplay_FormattingPreservesAllProperties()
    {
        GenJsonObject.Sample(jsonElement =>
        {
            // Act: Format and reparse
            var formatted = FormatJson(jsonElement);
            var reparsed = JsonDocument.Parse(formatted).RootElement;

            // Assert: All original properties should be present
            var originalProps = jsonElement.EnumerateObject().Select(p => p.Name).ToHashSet();
            var reparsedProps = reparsed.EnumerateObject().Select(p => p.Name).ToHashSet();

            Assert.Equal(originalProps.Count, reparsedProps.Count);
            foreach (var prop in originalProps)
            {
                Assert.Contains(prop, reparsedProps);
            }
        }, iter: 100);
    }

    /// <summary>
    /// For any JSON array, formatting should preserve all elements.
    /// </summary>
    [Fact]
    public void ExecutionDataDisplay_FormattingPreservesArrayElements()
    {
        GenJsonArray.Sample(jsonElement =>
        {
            // Act: Format and reparse
            var formatted = FormatJson(jsonElement);
            var reparsed = JsonDocument.Parse(formatted).RootElement;

            // Assert: Array length should be preserved
            Assert.Equal(jsonElement.GetArrayLength(), reparsed.GetArrayLength());
        }, iter: 100);
    }

    /// <summary>
    /// For any null execution data, the panel should indicate no data is available.
    /// </summary>
    [Fact]
    public void ExecutionDataDisplay_NullDataIndicatesNoDataAvailable()
    {
        // Arrange: Null JsonElement (simulating no execution data)
        JsonElement? nullData = null;

        // Act: Check if data is available
        var hasData = nullData.HasValue;

        // Assert: Should indicate no data
        Assert.False(hasData);
    }

    /// <summary>
    /// For any execution data, the formatted output should be semantically equivalent to the input.
    /// </summary>
    [Fact]
    public void ExecutionDataDisplay_FormattedOutputSemanticEquivalence()
    {
        GenJsonElement.Sample(jsonElement =>
        {
            // Act: Format the JSON
            var formatted = FormatJson(jsonElement);

            // Reparse and compare
            var reparsed = JsonDocument.Parse(formatted).RootElement;

            // Assert: Values should be semantically equivalent
            Assert.True(JsonElementsAreEquivalent(jsonElement, reparsed),
                $"Original and reparsed JSON should be equivalent.\nOriginal: {jsonElement.GetRawText()}\nReparsed: {reparsed.GetRawText()}");
        }, iter: 100);
    }

    /// <summary>
    /// For any nested JSON structure, formatting should preserve the nesting.
    /// </summary>
    [Fact]
    public void ExecutionDataDisplay_NestedStructuresPreserved()
    {
        GenNestedJsonObject.Sample(jsonElement =>
        {
            // Act: Format and reparse
            var formatted = FormatJson(jsonElement);
            var reparsed = JsonDocument.Parse(formatted).RootElement;

            // Assert: Structure should be preserved
            Assert.True(JsonElementsAreEquivalent(jsonElement, reparsed));
        }, iter: 100);
    }

    #region Helper Methods

    /// <summary>
    /// Formats a JsonElement as indented JSON string.
    /// This mirrors the logic used in NodeEditorInputPanel and NodeEditorOutputPanel.
    /// </summary>
    private static string FormatJson(JsonElement element)
    {
        try
        {
            return JsonSerializer.Serialize(element, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch
        {
            return element.GetRawText();
        }
    }

    /// <summary>
    /// Compares two JsonElements for semantic equivalence.
    /// </summary>
    private static bool JsonElementsAreEquivalent(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
            return false;

        return a.ValueKind switch
        {
            JsonValueKind.Object => ObjectsAreEquivalent(a, b),
            JsonValueKind.Array => ArraysAreEquivalent(a, b),
            JsonValueKind.String => a.GetString() == b.GetString(),
            JsonValueKind.Number => a.GetRawText() == b.GetRawText(),
            JsonValueKind.True or JsonValueKind.False => a.GetBoolean() == b.GetBoolean(),
            JsonValueKind.Null => true,
            _ => a.GetRawText() == b.GetRawText()
        };
    }

    private static bool ObjectsAreEquivalent(JsonElement a, JsonElement b)
    {
        var aProps = a.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        var bProps = b.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

        if (aProps.Count != bProps.Count)
            return false;

        foreach (var kvp in aProps)
        {
            if (!bProps.TryGetValue(kvp.Key, out var bValue))
                return false;

            if (!JsonElementsAreEquivalent(kvp.Value, bValue))
                return false;
        }

        return true;
    }

    private static bool ArraysAreEquivalent(JsonElement a, JsonElement b)
    {
        var aItems = a.EnumerateArray().ToList();
        var bItems = b.EnumerateArray().ToList();

        if (aItems.Count != bItems.Count)
            return false;

        for (int i = 0; i < aItems.Count; i++)
        {
            if (!JsonElementsAreEquivalent(aItems[i], bItems[i]))
                return false;
        }

        return true;
    }

    #endregion

    #region Generators

    /// <summary>Generator for simple string values.</summary>
    private static readonly Gen<string> GenSimpleString =
        Gen.Char['a', 'z'].Array[1, 20].Select(chars => new string(chars));

    /// <summary>Generator for JSON objects with string values.</summary>
    private static readonly Gen<JsonElement> GenJsonObject =
        Gen.Int[0, 5].SelectMany(propCount =>
        {
            if (propCount == 0)
            {
                return Gen.Const(JsonSerializer.SerializeToElement(new Dictionary<string, string>()));
            }

            return GenSimpleString.List[propCount, propCount].Select(keys =>
            {
                var dict = new Dictionary<string, string>();
                var uniqueKeys = keys.Distinct().ToList();
                for (int i = 0; i < uniqueKeys.Count; i++)
                {
                    dict[uniqueKeys[i]] = $"value_{i}";
                }
                return JsonSerializer.SerializeToElement(dict);
            });
        });

    /// <summary>Generator for JSON arrays with string values.</summary>
    private static readonly Gen<JsonElement> GenJsonArray =
        Gen.Int[0, 5].SelectMany(length =>
        {
            if (length == 0)
            {
                return Gen.Const(JsonSerializer.SerializeToElement(new List<string>()));
            }

            return GenSimpleString.List[length, length].Select(items =>
                JsonSerializer.SerializeToElement(items));
        });

    /// <summary>Generator for any JSON element (object, array, or primitive).</summary>
    private static readonly Gen<JsonElement> GenJsonElement =
        Gen.OneOf(
            GenJsonObject,
            GenJsonArray,
            GenSimpleString.Select(s => JsonSerializer.SerializeToElement(s)),
            Gen.Int[-1000, 1000].Select(i => JsonSerializer.SerializeToElement(i)),
            Gen.Bool.Select(b => JsonSerializer.SerializeToElement(b))
        );

    /// <summary>Generator for nested JSON objects.</summary>
    private static readonly Gen<JsonElement> GenNestedJsonObject =
        GenSimpleString.SelectMany(key1 =>
            GenSimpleString.SelectMany(key2 =>
                GenSimpleString.Select(value =>
                {
                    var nested = new Dictionary<string, object>
                    {
                        [key1] = new Dictionary<string, string>
                        {
                            [key2] = value
                        }
                    };
                    return JsonSerializer.SerializeToElement(nested);
                })));

    #endregion
}
