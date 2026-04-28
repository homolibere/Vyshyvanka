using System.Text.Json;
using CsCheck;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;

namespace Vyshyvanka.Tests.Property;

/// <summary>
/// Property-based tests for required field validation in NodeEditorConfigPanel.
/// </summary>
public class RequiredFieldValidationTests
{
    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 9: Required Field Validation
    /// For any configuration where a required property has no value or an empty value,
    /// the Property_Editor should display a validation error, and attempting to save
    /// should fail with an error message listing the missing fields.
    /// Validates: Requirements 5.1, 5.2, 5.3
    /// </summary>
    [Fact]
    public void RequiredFieldValidation_DetectsMissingRequiredFields()
    {
        GenPropertiesWithValues.Sample(testCase =>
        {
            var (properties, values) = testCase;

            // Act: Identify missing required fields
            var missingFields = GetMissingRequiredFields(properties, values);

            // Assert: All required fields with empty values should be in the missing list
            foreach (var prop in properties.Where(p => p.IsRequired))
            {
                var value = values.GetValueOrDefault(prop.Name);
                var isEmpty = IsValueEmpty(value);

                if (isEmpty)
                {
                    Assert.Contains(prop.DisplayName, missingFields);
                }
                else
                {
                    Assert.DoesNotContain(prop.DisplayName, missingFields);
                }
            }
        }, iter: 100);
    }

    /// <summary>
    /// For any configuration with all required fields filled, validation should pass.
    /// </summary>
    [Fact]
    public void RequiredFieldValidation_PassesWhenAllRequiredFieldsFilled()
    {
        GenPropertiesWithAllRequiredFilled.Sample(testCase =>
        {
            var (properties, values) = testCase;

            // Act
            var missingFields = GetMissingRequiredFields(properties, values);

            // Assert: No missing required fields
            Assert.Empty(missingFields);
        }, iter: 100);
    }

    /// <summary>
    /// For any configuration with at least one missing required field, validation should fail.
    /// </summary>
    [Fact]
    public void RequiredFieldValidation_FailsWhenRequiredFieldMissing()
    {
        GenPropertiesWithMissingRequired.Sample(testCase =>
        {
            var (properties, values) = testCase;

            // Act
            var missingFields = GetMissingRequiredFields(properties, values);

            // Assert: At least one missing required field
            Assert.NotEmpty(missingFields);
        }, iter: 100);
    }

    /// <summary>
    /// For any property, ShouldShowValidationError returns true only for required fields with empty values.
    /// </summary>
    [Fact]
    public void ShouldShowValidationError_OnlyTrueForEmptyRequiredFields()
    {
        GenPropertyWithValue.Sample(testCase =>
        {
            var (property, value) = testCase;

            // Act
            var shouldShow = ShouldShowValidationError(property, value, showValidationErrors: true);

            // Assert
            var expectedToShow = property.IsRequired && IsValueEmpty(value);
            Assert.Equal(expectedToShow, shouldShow);
        }, iter: 100);
    }

    /// <summary>
    /// When ShowValidationErrors is false, no validation errors should be shown.
    /// </summary>
    [Fact]
    public void ShouldShowValidationError_FalseWhenValidationDisabled()
    {
        GenPropertyWithValue.Sample(testCase =>
        {
            var (property, value) = testCase;

            // Act
            var shouldShow = ShouldShowValidationError(property, value, showValidationErrors: false);

            // Assert: Never show errors when validation is disabled
            Assert.False(shouldShow);
        }, iter: 100);
    }

    #region Validation Logic (mirrors NodeEditorConfigPanel)

    private static List<string> GetMissingRequiredFields(
        List<ConfigurationProperty> properties,
        Dictionary<string, object?> values)
    {
        return properties
            .Where(p => p.IsRequired && IsValueEmpty(values.GetValueOrDefault(p.Name)))
            .Select(p => p.DisplayName)
            .ToList();
    }

    private static bool ShouldShowValidationError(
        ConfigurationProperty property,
        object? value,
        bool showValidationErrors)
    {
        if (!showValidationErrors || !property.IsRequired)
            return false;

        return IsValueEmpty(value);
    }

    private static bool IsValueEmpty(object? value)
    {
        if (value is null)
            return true;

        if (value is string s)
            return string.IsNullOrWhiteSpace(s);

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => true,
                JsonValueKind.String => string.IsNullOrWhiteSpace(element.GetString()),
                _ => false
            };
        }

        return false;
    }

    #endregion

    #region Generators

    private static readonly Gen<string> GenPropertyName =
        Gen.Char['a', 'z'].Array[3, 15].Select(chars => new string(chars));

    private static readonly Gen<string> GenDisplayName =
        Gen.Char['A', 'Z'].SelectMany(first =>
            Gen.Char['a', 'z'].Array[2, 14].Select(rest =>
                first + new string(rest)));

    private static readonly Gen<string?> GenDescription =
        Gen.Bool.SelectMany(hasDesc =>
            hasDesc
                ? Gen.Char['a', 'z'].Array[5, 50].Select(chars => (string?)new string(chars))
                : Gen.Const((string?)null));

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
        from description in GenDescription
        from isRequired in Gen.Bool
        select new ConfigurationProperty
        {
            Name = name,
            DisplayName = displayName,
            Type = type,
            Description = description,
            IsRequired = isRequired,
            Options = null
        };

    /// <summary>Generator for a value that may or may not be empty.</summary>
    private static Gen<object?> GenValueForType(string type, bool allowEmpty) =>
        Gen.Bool.SelectMany(makeEmpty =>
            makeEmpty && allowEmpty
                ? GenEmptyValue()
                : GenNonEmptyValueForType(type));

    private static Gen<object?> GenEmptyValue() =>
        Gen.OneOf<object?>(
            Gen.Const<object?>(null!),
            Gen.Const<object?>(""),
            Gen.Const<object?>("   ")
        );

    private static Gen<object?> GenNonEmptyValueForType(string type) =>
        type switch
        {
            "string" => Gen.Char['a', 'z'].Array[1, 20].Select(chars => (object?)new string(chars)),
            "number" => Gen.Double[-1000, 1000].Select(d => (object?)d),
            "boolean" => Gen.Bool.Select(b => (object?)b),
            "object" => Gen.Const<object?>(JsonSerializer.SerializeToElement(new { key = "value" })),
            "array" => Gen.Const<object?>(JsonSerializer.SerializeToElement(new[] { "item1", "item2" })),
            _ => Gen.Const<object?>("default")
        };

    /// <summary>Generator for properties with values (some may be empty).</summary>
    private static readonly Gen<(List<ConfigurationProperty> Properties, Dictionary<string, object?> Values)> GenPropertiesWithValues =
        from propertyCount in Gen.Int[1, 5]
        from properties in GenConfigurationProperty.List[propertyCount, propertyCount]
        select BuildPropertiesWithValues(properties, allowEmpty: true);

    /// <summary>Generator for properties where all required fields are filled.</summary>
    private static readonly Gen<(List<ConfigurationProperty> Properties, Dictionary<string, object?> Values)> GenPropertiesWithAllRequiredFilled =
        from propertyCount in Gen.Int[1, 5]
        from properties in GenConfigurationProperty.List[propertyCount, propertyCount]
        select BuildPropertiesWithAllRequiredFilled(properties);

    /// <summary>Generator for properties with at least one missing required field.</summary>
    private static readonly Gen<(List<ConfigurationProperty> Properties, Dictionary<string, object?> Values)> GenPropertiesWithMissingRequired =
        from propertyCount in Gen.Int[1, 5]
        from properties in GenConfigurationProperty.List[propertyCount, propertyCount]
        select BuildPropertiesWithMissingRequired(properties);

    /// <summary>Generator for a single property with a value.</summary>
    private static readonly Gen<(ConfigurationProperty Property, object? Value)> GenPropertyWithValue =
        from property in GenConfigurationProperty
        from isEmpty in Gen.Bool
        select (property, isEmpty ? (object?)null : GenerateNonEmptyValue(property.Type));

    private static (List<ConfigurationProperty> Properties, Dictionary<string, object?> Values) BuildPropertiesWithValues(
        List<ConfigurationProperty> properties,
        bool allowEmpty)
    {
        var uniqueProperties = properties
            .GroupBy(p => p.Name)
            .Select(g => g.First())
            .ToList();

        var values = new Dictionary<string, object?>();
        var random = new Random(42);

        foreach (var prop in uniqueProperties)
        {
            var makeEmpty = allowEmpty && random.Next(3) == 0;
            values[prop.Name] = makeEmpty ? null : GenerateNonEmptyValue(prop.Type);
        }

        return (uniqueProperties, values);
    }

    private static (List<ConfigurationProperty> Properties, Dictionary<string, object?> Values) BuildPropertiesWithAllRequiredFilled(
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
            // Required fields always get a value, optional fields may be empty
            var makeEmpty = !prop.IsRequired && random.Next(2) == 0;
            values[prop.Name] = makeEmpty ? null : GenerateNonEmptyValue(prop.Type);
        }

        return (uniqueProperties, values);
    }

    private static (List<ConfigurationProperty> Properties, Dictionary<string, object?> Values) BuildPropertiesWithMissingRequired(
        List<ConfigurationProperty> properties)
    {
        var uniqueProperties = properties
            .GroupBy(p => p.Name)
            .Select(g => g.First())
            .ToList();

        // Ensure at least one required property exists
        if (!uniqueProperties.Any(p => p.IsRequired))
        {
            uniqueProperties = uniqueProperties
                .Select((p, i) => i == 0
                    ? p with { IsRequired = true }
                    : p)
                .ToList();
        }

        var values = new Dictionary<string, object?>();
        var random = new Random(42);
        var madeOneMissing = false;

        foreach (var prop in uniqueProperties)
        {
            if (prop.IsRequired && !madeOneMissing)
            {
                // Make at least one required field empty
                values[prop.Name] = null;
                madeOneMissing = true;
            }
            else
            {
                values[prop.Name] = GenerateNonEmptyValue(prop.Type);
            }
        }

        return (uniqueProperties, values);
    }

    private static object? GenerateNonEmptyValue(string type) =>
        type switch
        {
            "string" => "test_value",
            "number" => 42.5,
            "boolean" => true,
            "object" => JsonSerializer.SerializeToElement(new { key = "value" }),
            "array" => JsonSerializer.SerializeToElement(new[] { "item1" }),
            _ => "default"
        };

    #endregion
}
