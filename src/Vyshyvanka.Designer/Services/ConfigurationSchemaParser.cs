using System.Text.Json;
using Vyshyvanka.Designer.Models;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// Utility for parsing node configuration schemas and managing configuration values.
/// </summary>
public static class ConfigurationSchemaParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Parses a JSON schema into a list of configuration properties.
    /// </summary>
    /// <param name="schema">The JSON schema element from NodeDefinition.ConfigurationSchema.</param>
    /// <returns>List of configuration properties, or empty list if schema is null/invalid.</returns>
    public static List<ConfigurationProperty> Parse(JsonElement? schema)
    {
        if (!schema.HasValue)
            return [];

        var properties = new List<ConfigurationProperty>();

        try
        {
            var schemaObj = schema.Value;

            // Get the properties object
            if (!schemaObj.TryGetProperty("properties", out var propertiesElement))
                return [];

            // Get required array
            var requiredSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (schemaObj.TryGetProperty("required", out var requiredElement) &&
                requiredElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in requiredElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var name = item.GetString();
                        if (!string.IsNullOrEmpty(name))
                            requiredSet.Add(name);
                    }
                }
            }

            // Parse each property
            foreach (var prop in propertiesElement.EnumerateObject())
            {
                var configProp = ParseProperty(prop.Name, prop.Value, requiredSet.Contains(prop.Name));
                properties.Add(configProp);
            }
        }
        catch (JsonException)
        {
            // Return empty list on parse error
            return [];
        }

        return properties;
    }

    /// <summary>
    /// Extracts current values from a configuration JSON element.
    /// </summary>
    /// <param name="config">The node's current configuration.</param>
    /// <param name="properties">The list of properties to extract values for.</param>
    /// <returns>Dictionary of property name to value.</returns>
    public static Dictionary<string, object?> ExtractValues(JsonElement? config, List<ConfigurationProperty> properties)
    {
        var values = new Dictionary<string, object?>();

        if (!config.HasValue || config.Value.ValueKind != JsonValueKind.Object)
        {
            // Initialize with null values for all properties
            foreach (var prop in properties)
            {
                values[prop.Name] = null;
            }

            return values;
        }

        foreach (var prop in properties)
        {
            if (config.Value.TryGetProperty(prop.Name, out var valueElement))
            {
                values[prop.Name] = ConvertJsonElementToValue(valueElement, prop.Type);
            }
            else
            {
                values[prop.Name] = null;
            }
        }

        return values;
    }

    /// <summary>
    /// Builds a JSON configuration element from a dictionary of values.
    /// </summary>
    /// <param name="values">Dictionary of property name to value.</param>
    /// <returns>JsonElement representing the configuration.</returns>
    public static JsonElement BuildConfiguration(Dictionary<string, object?> values)
    {
        var configDict = new Dictionary<string, object?>();

        foreach (var (key, value) in values)
        {
            if (value is null)
                continue;

            configDict[key] = value;
        }

        return JsonSerializer.SerializeToElement(configDict, SerializerOptions);
    }

    /// <summary>
    /// Converts a property name to a display name by adding spaces before capitals.
    /// </summary>
    /// <param name="name">The property name in camelCase or PascalCase.</param>
    /// <returns>A human-readable display name.</returns>
    public static string ToDisplayName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        // Handle common abbreviations
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
            {
                result.Append(' ');
            }

            result.Append(i == 0 ? char.ToUpper(c) : c);
        }

        return result.ToString();
    }

    private static ConfigurationProperty ParseProperty(string name, JsonElement propSchema, bool isRequired)
    {
        var type = "string";
        string? description = null;
        string? dataSource = null;
        List<string>? options = null;

        if (propSchema.TryGetProperty("type", out var typeElement) &&
            typeElement.ValueKind == JsonValueKind.String)
        {
            type = typeElement.GetString() ?? "string";
        }

        if (propSchema.TryGetProperty("description", out var descElement) &&
            descElement.ValueKind == JsonValueKind.String)
        {
            description = descElement.GetString();
        }

        if (propSchema.TryGetProperty("dataSource", out var dataSourceElement) &&
            dataSourceElement.ValueKind == JsonValueKind.String)
        {
            dataSource = dataSourceElement.GetString();
        }

        // Check for enum/options
        if (propSchema.TryGetProperty("enum", out var enumElement) &&
            enumElement.ValueKind == JsonValueKind.Array)
        {
            options = [];
            foreach (var item in enumElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var optionValue = item.GetString();
                    if (!string.IsNullOrEmpty(optionValue))
                        options.Add(optionValue);
                }
            }
        }
        // Also check for "options" as an alternative
        else if (propSchema.TryGetProperty("options", out var optionsElement) &&
                 optionsElement.ValueKind == JsonValueKind.Array)
        {
            options = [];
            foreach (var item in optionsElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var optionValue = item.GetString();
                    if (!string.IsNullOrEmpty(optionValue))
                        options.Add(optionValue);
                }
            }
        }

        return new ConfigurationProperty
        {
            Name = name,
            DisplayName = ToDisplayName(name),
            Type = type.ToLowerInvariant(),
            Description = description,
            IsRequired = isRequired,
            Options = options?.Count > 0 ? options : null,
            DataSource = dataSource
        };
    }

    private static object? ConvertJsonElementToValue(JsonElement element, string propertyType)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => propertyType switch
            {
                "integer" => element.TryGetInt64(out var intVal) ? intVal : element.GetDouble(),
                "number" => element.GetDouble(),
                _ => element.TryGetInt64(out var i) ? i : element.GetDouble()
            },
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Object => element.Clone(),
            JsonValueKind.Array => element.Clone(),
            _ => null
        };
    }
}
