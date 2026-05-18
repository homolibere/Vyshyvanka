using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Text.RegularExpressions;

namespace Vyshyvanka.Designer.Components;

/// <summary>
/// Property editor for string type configuration properties.
/// Supports expression syntax detection, visual indication, autocomplete
/// for {{ }} expressions with node references, variables, and functions,
/// and drag-and-drop of expressions from the JSON input viewer.
/// </summary>
public partial class StringPropertyEditor : ComponentBase
{
    private static readonly Regex ExpressionPattern =
        new(@"\{\{.*?\}\}", RegexOptions.Compiled);

    [Parameter, EditorRequired] public ConfigurationProperty Property { get; set; } = null!;

    [Parameter] public object? Value { get; set; }

    [Parameter] public EventCallback<object?> ValueChanged { get; set; }

    [Parameter] public bool ShowValidationError { get; set; }

    /// <summary>
    /// The ID of the node currently being edited. Used to exclude self from autocomplete suggestions.
    /// </summary>
    [Parameter]
    public string? CurrentNodeId { get; set; }

    [Inject] private ExpressionAutocompleteService AutocompleteService { get; set; } = default!;
    [Inject] private WorkflowStateService WorkflowState { get; set; } = default!;
    [Inject] private ExpressionDragService DragService { get; set; } = default!;

    private ElementReference _inputRef;
    private ExpressionAutocomplete? _autocomplete;
    private bool _showAutocomplete;
    private List<ExpressionSuggestion> _suggestions = [];
    private string? _currentExpressionFragment;
    private bool _isDragOver;

    private string CurrentValue => Value?.ToString() ?? string.Empty;

    private bool HasExpression => ContainsExpression(CurrentValue);

    /// <summary>
    /// Checks if the given value contains expression syntax ({{ ... }}).
    /// </summary>
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
        var newValue = e.Value?.ToString() ?? "";
        await ValueChanged.InvokeAsync(newValue);

        // Check if we're inside an open expression {{ ... (no closing }})
        UpdateAutocomplete(newValue);
    }

    private void OnBlur()
    {
        // Delay hiding to allow click on suggestion
        _ = HideAutocompleteDelayed();
    }

    private async Task HideAutocompleteDelayed()
    {
        await Task.Delay(200);
        _showAutocomplete = false;
        StateHasChanged();
    }

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (!_showAutocomplete || _autocomplete is null) return;

        switch (e.Key)
        {
            case "ArrowDown":
                _autocomplete.MoveDown();
                break;
            case "ArrowUp":
                _autocomplete.MoveUp();
                break;
            case "Enter" or "Tab":
                await _autocomplete.ConfirmSelection();
                break;
            case "Escape":
                _showAutocomplete = false;
                break;
        }
    }

    private void OnDragOver(DragEventArgs e)
    {
        if (DragService.IsDragging)
        {
            _isDragOver = true;
        }
    }

    private void OnDragLeave(DragEventArgs e)
    {
        _isDragOver = false;
    }

    private async Task OnDropAsync(DragEventArgs e)
    {
        _isDragOver = false;

        if (!DragService.IsDragging || DragService.CurrentExpression is null)
            return;

        var expression = DragService.CurrentExpression;
        DragService.EndDrag();

        // Append the expression to the current value (or replace if empty)
        var currentText = CurrentValue;
        var newValue = string.IsNullOrEmpty(currentText)
            ? expression
            : $"{currentText}{expression}";

        await ValueChanged.InvokeAsync(newValue);
    }

    private void UpdateAutocomplete(string value)
    {
        // Find the last unclosed {{ in the string
        var lastOpen = value.LastIndexOf("{{", StringComparison.Ordinal);
        if (lastOpen < 0)
        {
            _showAutocomplete = false;
            _suggestions = [];
            return;
        }

        // Check if this {{ has a matching }} after it
        var afterOpen = value[(lastOpen + 2)..];
        if (afterOpen.Contains("}}", StringComparison.Ordinal))
        {
            // The last {{ is already closed — no autocomplete needed
            _showAutocomplete = false;
            _suggestions = [];
            return;
        }

        // We're inside an open expression — extract the fragment after {{
        _currentExpressionFragment = afterOpen.TrimStart();
        var nodeId = CurrentNodeId ?? WorkflowState.SelectedNodeId;
        _suggestions = AutocompleteService.GetSuggestions(_currentExpressionFragment, nodeId);
        _showAutocomplete = _suggestions.Count > 0;
        _autocomplete?.ResetSelection();
    }

    private async Task OnSuggestionSelected(ExpressionSuggestion suggestion)
    {
        var currentText = CurrentValue;

        // Find the last unclosed {{ to know where to insert
        var lastOpen = currentText.LastIndexOf("{{", StringComparison.Ordinal);
        if (lastOpen < 0)
        {
            _showAutocomplete = false;
            return;
        }

        var beforeExpression = currentText[..lastOpen];
        var insertText = suggestion.InsertText;

        // If the suggestion doesn't end with a dot or open paren, close the expression
        var needsClosing = !insertText.EndsWith('.') && !insertText.EndsWith('(');
        var newValue = needsClosing
            ? $"{beforeExpression}{{{{ {insertText} }}}}"
            : $"{beforeExpression}{{{{ {insertText}";

        await ValueChanged.InvokeAsync(newValue);

        // If suggestion ends with dot, keep autocomplete open for next level
        if (insertText.EndsWith('.'))
        {
            UpdateAutocomplete(newValue);
        }
        else
        {
            _showAutocomplete = false;
        }
    }
}
