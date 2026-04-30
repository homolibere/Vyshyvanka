using Vyshyvanka.Designer.Models;
using Microsoft.AspNetCore.Components;
using System.Text.RegularExpressions;

namespace Vyshyvanka.Designer.Components;

/// <summary>
/// Property editor for string type configuration properties.
/// Supports expression syntax detection and visual indication.
/// </summary>
public partial class StringPropertyEditor : ComponentBase
{
    private static readonly Regex ExpressionPattern = 
        new(@"\{\{.*?\}\}", RegexOptions.Compiled);

    [Parameter, EditorRequired]
    public ConfigurationProperty Property { get; set; } = null!;

    [Parameter]
    public object? Value { get; set; }

    [Parameter]
    public EventCallback<object?> ValueChanged { get; set; }

    [Parameter]
    public bool ShowValidationError { get; set; }

    private string CurrentValue => Value?.ToString() ?? string.Empty;

    private bool HasExpression => ContainsExpression(CurrentValue);

    /// <summary>
    /// Checks if the given value contains expression syntax ({{ ... }}).
    /// </summary>
    /// <param name="value">The string value to check.</param>
    /// <returns>True if the value contains expression syntax, false otherwise.</returns>
    public static bool ContainsExpression(string? value)
    {
        return !string.IsNullOrEmpty(value) && ExpressionPattern.IsMatch(value);
    }

    private string GetPlaceholder()
    {
        if (HasExpression)
            return "Expression value";
        return Property.IsRequired ? "Required" : "Optional";
    }

    private async Task OnInput(ChangeEventArgs e)
    {
        var newValue = e.Value?.ToString();
        await ValueChanged.InvokeAsync(newValue);
    }

    private async Task OnBlur()
    {
        // Trigger validation on blur by re-emitting the current value
        await ValueChanged.InvokeAsync(Value);
    }
}
