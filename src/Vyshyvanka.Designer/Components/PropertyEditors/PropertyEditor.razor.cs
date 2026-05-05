using Vyshyvanka.Designer.Models;
using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Components;

/// <summary>
/// Factory component that renders the appropriate property editor based on property type.
/// Delegates to StringPropertyEditor, NumberPropertyEditor, BooleanPropertyEditor,
/// JsonPropertyEditor, or SelectPropertyEditor based on the property's type and options.
/// </summary>
public partial class PropertyEditor : ComponentBase
{
    /// <summary>
    /// The configuration property definition containing type, name, and metadata.
    /// </summary>
    [Parameter, EditorRequired]
    public ConfigurationProperty Property { get; set; } = null!;

    /// <summary>
    /// The current value of the property.
    /// </summary>
    [Parameter]
    public object? Value { get; set; }

    /// <summary>
    /// Callback invoked when the property value changes.
    /// </summary>
    [Parameter]
    public EventCallback<object?> ValueChanged { get; set; }

    /// <summary>
    /// Whether to show validation error state on the editor.
    /// </summary>
    [Parameter]
    public bool ShowValidationError { get; set; }

    /// <summary>
    /// All sibling configuration values. Used by editors that need context from other properties
    /// (e.g., CodePropertyEditor reads the "language" property).
    /// </summary>
    [Parameter]
    public Dictionary<string, object?>? SiblingValues { get; set; }

    /// <summary>
    /// Determines the editor type to render based on property type and options.
    /// </summary>
    internal EditorType GetEditorType()
    {
        // Dynamic data source -> specialized editor
        if (!string.IsNullOrEmpty(Property.DataSource))
        {
            return Property.DataSource switch
            {
                "workflows" => EditorType.WorkflowSelect,
                _ => EditorType.String
            };
        }

        // String with options -> Select dropdown
        if (Property.Type.Equals("string", StringComparison.OrdinalIgnoreCase) &&
            Property.Options is { Count: > 0 })
        {
            return EditorType.Select;
        }

        return Property.Type.ToLowerInvariant() switch
        {
            "string" => EditorType.String,
            "number" or "integer" => EditorType.Number,
            "boolean" => EditorType.Boolean,
            "object" or "array" => EditorType.Json,
            "code" => EditorType.Code,
            _ => EditorType.String // Default fallback for unknown types
        };
    }

    private async Task OnValueChanged(object? newValue)
    {
        await ValueChanged.InvokeAsync(newValue);
    }
}

/// <summary>
/// Enum representing the different editor component types.
/// </summary>
public enum EditorType
{
    String,
    Number,
    Boolean,
    Json,
    Select,
    WorkflowSelect,
    Code
}
