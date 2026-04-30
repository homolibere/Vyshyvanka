using Vyshyvanka.Designer.Models;
using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace Vyshyvanka.Designer.Components;

/// <summary>
/// Property editor for number type configuration properties.
/// Validates numeric input and displays errors for invalid values.
/// </summary>
public partial class NumberPropertyEditor : ComponentBase
{
    [Parameter, EditorRequired]
    public ConfigurationProperty Property { get; set; } = null!;

    [Parameter]
    public object? Value { get; set; }

    [Parameter]
    public EventCallback<object?> ValueChanged { get; set; }

    [Parameter]
    public bool ShowValidationError { get; set; }

    private string _inputValue = string.Empty;
    private string? _typeValidationError;

    private string DisplayValue => _inputValue;

    private bool HasValidationError => ShowValidationError || !string.IsNullOrEmpty(_typeValidationError);

    private string ValidationMessage
    {
        get
        {
            if (!string.IsNullOrEmpty(_typeValidationError))
                return _typeValidationError;
            if (ShowValidationError)
                return "This field is required";
            return string.Empty;
        }
    }

    protected override void OnParametersSet()
    {
        // Sync input value with parameter when it changes externally
        if (Value is not null)
        {
            _inputValue = Convert.ToString(Value, CultureInfo.InvariantCulture) ?? string.Empty;
        }
        else if (string.IsNullOrEmpty(_inputValue))
        {
            _inputValue = string.Empty;
        }
    }

    private string GetPlaceholder()
    {
        return Property.IsRequired ? "Required" : "Optional";
    }

    private async Task OnInput(ChangeEventArgs e)
    {
        _inputValue = e.Value?.ToString() ?? string.Empty;
        _typeValidationError = null;

        if (string.IsNullOrWhiteSpace(_inputValue))
        {
            await ValueChanged.InvokeAsync(null);
            return;
        }

        // Try to parse as number
        if (TryParseNumber(_inputValue, out var number))
        {
            await ValueChanged.InvokeAsync(number);
        }
        else
        {
            _typeValidationError = "Please enter a valid number";
        }
    }

    private async Task OnBlur()
    {
        // Validate on blur
        if (!string.IsNullOrWhiteSpace(_inputValue) && !TryParseNumber(_inputValue, out _))
        {
            _typeValidationError = "Please enter a valid number";
        }
        await ValueChanged.InvokeAsync(Value);
    }

    private static bool TryParseNumber(string input, out object? result)
    {
        result = null;
        
        if (string.IsNullOrWhiteSpace(input))
            return false;

        // Try integer first
        if (long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            // Use int if it fits, otherwise long
            if (longValue >= int.MinValue && longValue <= int.MaxValue)
                result = (int)longValue;
            else
                result = longValue;
            return true;
        }

        // Try decimal/float
        if (double.TryParse(input, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
        {
            result = doubleValue;
            return true;
        }

        return false;
    }
}
