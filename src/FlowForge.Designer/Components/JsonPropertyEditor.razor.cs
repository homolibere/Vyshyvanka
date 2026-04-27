using FlowForge.Designer.Models;
using Microsoft.AspNetCore.Components;
using System.Text.Json;

namespace FlowForge.Designer.Components;

/// <summary>
/// Property editor for object and array type configuration properties.
/// Renders a textarea with JSON syntax validation on blur.
/// </summary>
public partial class JsonPropertyEditor : ComponentBase
{
    [Parameter, EditorRequired]
    public ConfigurationProperty Property { get; set; } = null!;

    [Parameter]
    public object? Value { get; set; }

    [Parameter]
    public EventCallback<object?> ValueChanged { get; set; }

    [Parameter]
    public bool ShowValidationError { get; set; }

    private string _jsonText = string.Empty;
    private string? _syntaxError;

    private bool HasValidationError => ShowValidationError || !string.IsNullOrEmpty(_syntaxError);

    private string ValidationMessage
    {
        get
        {
            if (!string.IsNullOrEmpty(_syntaxError))
                return _syntaxError;
            if (ShowValidationError)
                return "This field is required";
            return string.Empty;
        }
    }

    protected override void OnParametersSet()
    {
        // Convert value to formatted JSON string
        if (Value is JsonElement element)
        {
            try
            {
                _jsonText = JsonSerializer.Serialize(element, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                _jsonText = element.GetRawText();
            }
        }
        else if (Value is string s)
        {
            _jsonText = s;
        }
        else if (Value is not null)
        {
            try
            {
                _jsonText = JsonSerializer.Serialize(Value, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                _jsonText = Value.ToString() ?? string.Empty;
            }
        }
        else
        {
            _jsonText = GetDefaultValue();
        }
    }

    private string GetTypeBadge() => Property.Type.ToUpperInvariant() switch
    {
        "OBJECT" => "{ }",
        "ARRAY" => "[ ]",
        _ => "JSON"
    };

    private string GetPlaceholder() => Property.Type.ToUpperInvariant() switch
    {
        "OBJECT" => "{ \"key\": \"value\" }",
        "ARRAY" => "[ \"item1\", \"item2\" ]",
        _ => "Enter JSON..."
    };

    private string GetDefaultValue() => Property.Type.ToUpperInvariant() switch
    {
        "OBJECT" => "{}",
        "ARRAY" => "[]",
        _ => ""
    };

    private async Task OnBlur()
    {
        _syntaxError = null;

        if (string.IsNullOrWhiteSpace(_jsonText))
        {
            await ValueChanged.InvokeAsync(null);
            return;
        }

        try
        {
            // Validate JSON syntax
            var doc = JsonDocument.Parse(_jsonText);
            var element = doc.RootElement;

            // Validate type matches expected
            var isValid = Property.Type.ToUpperInvariant() switch
            {
                "OBJECT" => element.ValueKind == JsonValueKind.Object,
                "ARRAY" => element.ValueKind == JsonValueKind.Array,
                _ => true
            };

            if (!isValid)
            {
                _syntaxError = $"Expected {Property.Type}, got {element.ValueKind}";
                return;
            }

            // Clone the element to avoid disposal issues
            await ValueChanged.InvokeAsync(element.Clone());
        }
        catch (JsonException ex)
        {
            _syntaxError = $"Invalid JSON: {ex.Message}";
        }
    }
}
