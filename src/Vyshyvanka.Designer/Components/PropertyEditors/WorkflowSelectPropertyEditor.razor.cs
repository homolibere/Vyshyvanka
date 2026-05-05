using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Components;

/// <summary>
/// Property editor that displays a dropdown of available workflows.
/// Fetches the workflow list from the API on initialization.
/// </summary>
public partial class WorkflowSelectPropertyEditor : ComponentBase
{
    [Inject] private VyshyvankaApiClient ApiClient { get; set; } = null!;

    [Parameter, EditorRequired] public ConfigurationProperty Property { get; set; } = null!;

    [Parameter] public object? Value { get; set; }

    [Parameter] public EventCallback<object?> ValueChanged { get; set; }

    [Parameter] public bool ShowValidationError { get; set; }

    private List<WorkflowOption> _workflows = [];
    private bool _isLoading;
    private string? _error;

    private string CurrentValue => Value?.ToString() ?? string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadWorkflowsAsync();
    }

    private async Task LoadWorkflowsAsync()
    {
        _isLoading = true;
        _error = null;

        try
        {
            var workflows = await ApiClient.GetWorkflowsAsync();
            _workflows = workflows
                .Where(w => w.IsActive)
                .OrderBy(w => w.Name)
                .Select(w => new WorkflowOption(w.Id, w.Name))
                .ToList();
        }
        catch (Exception ex)
        {
            _error = $"Failed to load workflows: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OnChange(ChangeEventArgs e)
    {
        var newValue = e.Value?.ToString();

        if (string.IsNullOrEmpty(newValue))
        {
            await ValueChanged.InvokeAsync(null);
        }
        else
        {
            await ValueChanged.InvokeAsync(newValue);
        }
    }

    private record WorkflowOption(Guid Id, string Name);
}
