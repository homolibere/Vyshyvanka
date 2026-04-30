using System.Text.Json;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Components;

/// <summary>
/// Configuration panel component for the node editor modal.
/// Renders PropertyEditor for each schema property and supports form/JSON view toggle.
/// </summary>
public partial class NodeEditorConfigPanel : ComponentBase
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    /// <summary>
    /// List of configuration properties parsed from the node's schema.
    /// </summary>
    [Parameter, EditorRequired]
    public List<ConfigurationProperty> Properties { get; set; } = [];

    /// <summary>
    /// Current configuration values by property name.
    /// </summary>
    [Parameter, EditorRequired]
    public Dictionary<string, object?> Values { get; set; } = new();

    /// <summary>
    /// Callback invoked when a property value changes.
    /// </summary>
    [Parameter]
    public EventCallback<(string PropertyName, object? Value)> OnValueChanged { get; set; }

    /// <summary>
    /// Callback invoked when all values change (e.g., from JSON mode).
    /// </summary>
    [Parameter]
    public EventCallback<Dictionary<string, object?>> OnValuesChanged { get; set; }

    /// <summary>
    /// Whether the panel is in JSON editing mode.
    /// </summary>
    [Parameter]
    public bool IsJsonMode { get; set; }

    /// <summary>
    /// Callback invoked when the mode changes.
    /// </summary>
    [Parameter]
    public EventCallback<bool> IsJsonModeChanged { get; set; }

    /// <summary>
    /// Whether to show validation errors for required fields.
    /// </summary>
    [Parameter]
    public bool ShowValidationErrors { get; set; }

    /// <summary>
    /// Whether the node has a configuration schema.
    /// When false, only the JSON editor is shown without toggle buttons.
    /// </summary>
    [Parameter]
    public bool HasSchema { get; set; } = true;

    /// <summary>
    /// Initial raw JSON for schema-less nodes.
    /// </summary>
    [Parameter]
    public string? InitialRawJson { get; set; }

    private string _rawJson = "{}";
    private string? _jsonError;

    private bool HasJsonError => !string.IsNullOrEmpty(_jsonError);
    private bool HasValidationErrors => ShowValidationErrors && GetMissingRequiredFields().Any();

    protected override void OnParametersSet()
    {
        if (IsJsonMode)
        {
            // Update raw JSON from values when switching to JSON mode
            // For schema-less nodes, use InitialRawJson if available
            if (!HasSchema && !string.IsNullOrEmpty(InitialRawJson) && _rawJson == "{}")
            {
                _rawJson = InitialRawJson;
            }
            else
            {
                UpdateRawJsonFromValues();
            }
        }
    }

    /// <summary>
    /// Gets the current value for a property.
    /// </summary>
    internal object? GetValue(string propertyName)
    {
        return Values.TryGetValue(propertyName, out var value) ? value : null;
    }

    /// <summary>
    /// Determines if validation error should be shown for a property.
    /// </summary>
    internal bool ShouldShowValidationError(ConfigurationProperty property)
    {
        if (!ShowValidationErrors || !property.IsRequired)
            return false;

        var value = GetValue(property.Name);
        return IsValueEmpty(value);
    }

    /// <summary>
    /// Gets the list of missing required field names.
    /// </summary>
    internal List<string> GetMissingRequiredFields()
    {
        return Properties
            .Where(p => p.IsRequired && IsValueEmpty(GetValue(p.Name)))
            .Select(p => p.DisplayName)
            .ToList();
    }

    /// <summary>
    /// Validates all required fields and returns true if all are filled.
    /// </summary>
    public bool ValidateRequiredFields()
    {
        return !GetMissingRequiredFields().Any();
    }

    /// <summary>
    /// Gets the current raw JSON string (for JSON mode).
    /// </summary>
    public string GetRawJson() => _rawJson;

    /// <summary>
    /// Checks if there's a JSON parsing error.
    /// </summary>
    public bool HasParseError() => HasJsonError;

    /// <summary>
    /// Gets the JSON error message if any.
    /// </summary>
    public string? GetJsonError() => _jsonError;

    private async Task OnPropertyChanged(string propertyName, object? value)
    {
        await OnValueChanged.InvokeAsync((propertyName, value));
    }

    private async Task SetMode(bool jsonMode)
    {
        if (jsonMode == IsJsonMode)
            return;

        if (jsonMode)
        {
            // Switching to JSON mode - serialize current values
            UpdateRawJsonFromValues();
            _jsonError = null;
        }
        else
        {
            // Switching to form mode - parse JSON and populate values
            if (!TryParseJsonToValues(out var newValues, out var error))
            {
                _jsonError = error;
                return; // Stay in JSON mode on error
            }

            _jsonError = null;
            await OnValuesChanged.InvokeAsync(newValues);
        }

        await IsJsonModeChanged.InvokeAsync(jsonMode);
    }

    private void UpdateRawJsonFromValues()
    {
        try
        {
            var config = ConfigurationSchemaParser.BuildConfiguration(Values);
            _rawJson = JsonSerializer.Serialize(config, IndentedOptions);
        }
        catch
        {
            _rawJson = "{}";
        }
    }

    private bool TryParseJsonToValues(out Dictionary<string, object?> values, out string? error)
    {
        values = new Dictionary<string, object?>();
        error = null;

        if (string.IsNullOrWhiteSpace(_rawJson))
        {
            // Empty JSON is valid, just return empty values
            foreach (var prop in Properties)
            {
                values[prop.Name] = null;
            }
            return true;
        }

        try
        {
            var doc = JsonDocument.Parse(_rawJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "Configuration must be a JSON object";
                return false;
            }

            values = ConfigurationSchemaParser.ExtractValues(doc.RootElement, Properties);
            return true;
        }
        catch (JsonException ex)
        {
            error = $"Invalid JSON: {ex.Message}";
            return false;
        }
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
}
