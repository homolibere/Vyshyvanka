using Vyshyvanka.Designer.Models;
using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Components;

/// <summary>
/// Property editor for boolean type configuration properties.
/// Renders a toggle switch control for true/false values.
/// </summary>
public partial class BooleanPropertyEditor : ComponentBase
{
    [Parameter, EditorRequired]
    public ConfigurationProperty Property { get; set; } = null!;

    [Parameter]
    public object? Value { get; set; }

    [Parameter]
    public EventCallback<object?> ValueChanged { get; set; }

    [Parameter]
    public bool ShowValidationError { get; set; }

    private bool IsChecked => Value switch
    {
        bool b => b,
        string s when bool.TryParse(s, out var parsed) => parsed,
        _ => false
    };

    private async Task OnToggle(ChangeEventArgs e)
    {
        var newValue = e.Value is bool b ? b : false;
        await ValueChanged.InvokeAsync(newValue);
    }
}
