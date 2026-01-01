using FlowForge.Designer.Models;
using Microsoft.AspNetCore.Components;

namespace FlowForge.Designer.Components;

/// <summary>
/// Property editor for string properties with predefined options.
/// Renders a dropdown select control.
/// </summary>
public partial class SelectPropertyEditor : ComponentBase
{
    [Parameter, EditorRequired]
    public ConfigurationProperty Property { get; set; } = null!;

    [Parameter]
    public object? Value { get; set; }

    [Parameter]
    public EventCallback<object?> ValueChanged { get; set; }

    [Parameter]
    public bool ShowValidationError { get; set; }

    private string CurrentValue => Value?.ToString() ?? string.Empty;

    private async Task OnChange(ChangeEventArgs e)
    {
        var newValue = e.Value?.ToString();
        
        // Treat empty string as null for optional fields
        if (string.IsNullOrEmpty(newValue))
        {
            await ValueChanged.InvokeAsync(null);
        }
        else
        {
            await ValueChanged.InvokeAsync(newValue);
        }
    }
}
