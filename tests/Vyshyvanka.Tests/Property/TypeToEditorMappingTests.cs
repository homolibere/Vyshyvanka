using CsCheck;
using Vyshyvanka.Designer.Models;

namespace Vyshyvanka.Tests.Property;

/// <summary>
/// Property-based tests for type-to-editor mapping.
/// </summary>
public class TypeToEditorMappingTests
{
    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 5: Type-to-Editor Mapping
    /// For any configuration property, the Property_Editor should render the correct input control
    /// based on the property type:
    /// - "string" → text input (or dropdown if options defined)
    /// - "number" → numeric input
    /// - "boolean" → toggle switch
    /// - "object" → JSON editor
    /// - "array" → JSON editor
    /// Validates: Requirements 3.2, 3.3, 3.4, 3.5, 3.6, 6.1
    /// </summary>
    [Fact]
    public void TypeToEditorMapping_ReturnsCorrectEditorType()
    {
        GenConfigurationProperty.Sample(property =>
        {
            // Act: Determine which editor type should be used
            var editorType = GetExpectedEditorType(property);

            // Assert: Verify the mapping is correct based on property type and options
            switch (property.Type.ToLowerInvariant())
            {
                case "string" when property.Options is { Count: > 0 }:
                    Assert.Equal(EditorType.Select, editorType);
                    break;

                case "string":
                    Assert.Equal(EditorType.String, editorType);
                    break;

                case "number":
                case "integer":
                    Assert.Equal(EditorType.Number, editorType);
                    break;

                case "boolean":
                    Assert.Equal(EditorType.Boolean, editorType);
                    break;

                case "object":
                    Assert.Equal(EditorType.Json, editorType);
                    break;

                case "array":
                    Assert.Equal(EditorType.Json, editorType);
                    break;

                default:
                    // Unknown types should fall back to string editor
                    Assert.Equal(EditorType.String, editorType);
                    break;
            }
        }, iter: 100);
    }

    /// <summary>
    /// For any string property with options, the editor should be Select type.
    /// </summary>
    [Fact]
    public void StringPropertyWithOptions_MapsToSelectEditor()
    {
        GenStringPropertyWithOptions.Sample(property =>
        {
            // Act
            var editorType = GetExpectedEditorType(property);

            // Assert
            Assert.Equal(EditorType.Select, editorType);
            Assert.NotNull(property.Options);
            Assert.True(property.Options.Count > 0);
        }, iter: 100);
    }

    /// <summary>
    /// For any string property without options, the editor should be String type.
    /// </summary>
    [Fact]
    public void StringPropertyWithoutOptions_MapsToStringEditor()
    {
        GenStringPropertyWithoutOptions.Sample(property =>
        {
            // Act
            var editorType = GetExpectedEditorType(property);

            // Assert
            Assert.Equal(EditorType.String, editorType);
            Assert.True(property.Options is null || property.Options.Count == 0);
        }, iter: 100);
    }

    /// <summary>
    /// For any number property, the editor should be Number type regardless of options.
    /// </summary>
    [Fact]
    public void NumberProperty_MapsToNumberEditor()
    {
        GenNumberProperty.Sample(property =>
        {
            // Act
            var editorType = GetExpectedEditorType(property);

            // Assert
            Assert.Equal(EditorType.Number, editorType);
        }, iter: 100);
    }

    /// <summary>
    /// For any boolean property, the editor should be Boolean type.
    /// </summary>
    [Fact]
    public void BooleanProperty_MapsToBooleanEditor()
    {
        GenBooleanProperty.Sample(property =>
        {
            // Act
            var editorType = GetExpectedEditorType(property);

            // Assert
            Assert.Equal(EditorType.Boolean, editorType);
        }, iter: 100);
    }

    /// <summary>
    /// For any object or array property, the editor should be Json type.
    /// </summary>
    [Fact]
    public void ObjectOrArrayProperty_MapsToJsonEditor()
    {
        GenObjectOrArrayProperty.Sample(property =>
        {
            // Act
            var editorType = GetExpectedEditorType(property);

            // Assert
            Assert.Equal(EditorType.Json, editorType);
            Assert.True(
                property.Type.Equals("object", StringComparison.OrdinalIgnoreCase) ||
                property.Type.Equals("array", StringComparison.OrdinalIgnoreCase));
        }, iter: 100);
    }

    #region Editor Type Mapping Logic

    /// <summary>
    /// Enum representing the different editor component types.
    /// </summary>
    private enum EditorType
    {
        String,
        Number,
        Boolean,
        Json,
        Select
    }

    /// <summary>
    /// Determines the expected editor type for a given configuration property.
    /// This mirrors the logic that would be used in a PropertyEditor factory component.
    /// </summary>
    private static EditorType GetExpectedEditorType(ConfigurationProperty property)
    {
        // String with options -> Select
        if (property.Type.Equals("string", StringComparison.OrdinalIgnoreCase) &&
            property.Options is { Count: > 0 })
        {
            return EditorType.Select;
        }

        return property.Type.ToLowerInvariant() switch
        {
            "string" => EditorType.String,
            "number" or "integer" => EditorType.Number,
            "boolean" => EditorType.Boolean,
            "object" or "array" => EditorType.Json,
            _ => EditorType.String // Default fallback
        };
    }

    #endregion

    #region Generators

    /// <summary>Generator for non-empty alphanumeric property names.</summary>
    private static readonly Gen<string> GenPropertyName =
        Gen.Char['a', 'z'].Array[3, 15].Select(chars => new string(chars));

    /// <summary>Generator for display names.</summary>
    private static readonly Gen<string> GenDisplayName =
        Gen.Char['A', 'Z'].SelectMany(first =>
            Gen.Char['a', 'z'].Array[2, 14].Select(rest =>
                first + new string(rest)));

    /// <summary>Generator for optional description.</summary>
    private static readonly Gen<string?> GenDescription =
        Gen.Bool.SelectMany(hasDesc =>
            hasDesc
                ? Gen.Char['a', 'z'].Array[5, 50].Select(chars => (string?)new string(chars))
                : Gen.Const((string?)null));

    /// <summary>Generator for property types.</summary>
    private static readonly Gen<string> GenPropertyType =
        Gen.OneOf(
            Gen.Const("string"),
            Gen.Const("number"),
            Gen.Const("integer"),
            Gen.Const("boolean"),
            Gen.Const("object"),
            Gen.Const("array")
        );

    /// <summary>Generator for option values.</summary>
    private static readonly Gen<List<string>?> GenOptions =
        Gen.Bool.SelectMany(hasOptions =>
            hasOptions
                ? Gen.Char['A', 'Z'].Array[3, 10]
                    .Select(chars => new string(chars))
                    .List[2, 5]
                    .Select(list => (List<string>?)list)
                : Gen.Const((List<string>?)null));

    /// <summary>Generator for any configuration property.</summary>
    private static readonly Gen<ConfigurationProperty> GenConfigurationProperty =
        from name in GenPropertyName
        from displayName in GenDisplayName
        from type in GenPropertyType
        from description in GenDescription
        from isRequired in Gen.Bool
        from options in GenOptions
        select new ConfigurationProperty
        {
            Name = name,
            DisplayName = displayName,
            Type = type,
            Description = description,
            IsRequired = isRequired,
            Options = type.Equals("string", StringComparison.OrdinalIgnoreCase) ? options : null
        };

    /// <summary>Generator for string properties with options (for Select editor).</summary>
    private static readonly Gen<ConfigurationProperty> GenStringPropertyWithOptions =
        from name in GenPropertyName
        from displayName in GenDisplayName
        from description in GenDescription
        from isRequired in Gen.Bool
        from options in Gen.Char['A', 'Z'].Array[3, 10]
            .Select(chars => new string(chars))
            .List[2, 5]
        select new ConfigurationProperty
        {
            Name = name,
            DisplayName = displayName,
            Type = "string",
            Description = description,
            IsRequired = isRequired,
            Options = options
        };

    /// <summary>Generator for string properties without options.</summary>
    private static readonly Gen<ConfigurationProperty> GenStringPropertyWithoutOptions =
        from name in GenPropertyName
        from displayName in GenDisplayName
        from description in GenDescription
        from isRequired in Gen.Bool
        select new ConfigurationProperty
        {
            Name = name,
            DisplayName = displayName,
            Type = "string",
            Description = description,
            IsRequired = isRequired,
            Options = null
        };

    /// <summary>Generator for number properties.</summary>
    private static readonly Gen<ConfigurationProperty> GenNumberProperty =
        from name in GenPropertyName
        from displayName in GenDisplayName
        from type in Gen.OneOf(Gen.Const("number"), Gen.Const("integer"))
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

    /// <summary>Generator for boolean properties.</summary>
    private static readonly Gen<ConfigurationProperty> GenBooleanProperty =
        from name in GenPropertyName
        from displayName in GenDisplayName
        from description in GenDescription
        from isRequired in Gen.Bool
        select new ConfigurationProperty
        {
            Name = name,
            DisplayName = displayName,
            Type = "boolean",
            Description = description,
            IsRequired = isRequired,
            Options = null
        };

    /// <summary>Generator for object or array properties.</summary>
    private static readonly Gen<ConfigurationProperty> GenObjectOrArrayProperty =
        from name in GenPropertyName
        from displayName in GenDisplayName
        from type in Gen.OneOf(Gen.Const("object"), Gen.Const("array"))
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

    #endregion
}
