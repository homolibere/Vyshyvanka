using Microsoft.AspNetCore.Components;
using Vyshyvanka.Designer.Services;

namespace Vyshyvanka.Designer.Components;

/// <summary>
/// Dropdown autocomplete component for expression syntax inside {{ }}.
/// Shows contextual suggestions for node references, variables, and functions.
/// </summary>
public partial class ExpressionAutocomplete : ComponentBase
{
    /// <summary>Whether the autocomplete dropdown is visible.</summary>
    [Parameter]
    public bool IsVisible { get; set; }

    /// <summary>The list of suggestions to display.</summary>
    [Parameter]
    public List<ExpressionSuggestion> Suggestions { get; set; } = [];

    /// <summary>Callback when a suggestion is selected.</summary>
    [Parameter]
    public EventCallback<ExpressionSuggestion> OnSuggestionSelected { get; set; }

    /// <summary>Currently highlighted suggestion index.</summary>
    public int SelectedIndex { get; set; }

    /// <summary>
    /// Moves the selection up in the list.
    /// </summary>
    public void MoveUp()
    {
        if (Suggestions.Count == 0) return;
        SelectedIndex = (SelectedIndex - 1 + Suggestions.Count) % Suggestions.Count;
        StateHasChanged();
    }

    /// <summary>
    /// Moves the selection down in the list.
    /// </summary>
    public void MoveDown()
    {
        if (Suggestions.Count == 0) return;
        SelectedIndex = (SelectedIndex + 1) % Suggestions.Count;
        StateHasChanged();
    }

    /// <summary>
    /// Confirms the currently selected suggestion.
    /// </summary>
    public async Task ConfirmSelection()
    {
        if (Suggestions.Count == 0) return;
        var suggestion = Suggestions[Math.Clamp(SelectedIndex, 0, Suggestions.Count - 1)];
        await SelectSuggestion(suggestion);
    }

    /// <summary>
    /// Resets the selection index to the top.
    /// </summary>
    public void ResetSelection()
    {
        SelectedIndex = 0;
    }

    private async Task SelectSuggestion(ExpressionSuggestion suggestion)
    {
        await OnSuggestionSelected.InvokeAsync(suggestion);
    }

    private static string GetIcon(SuggestionKind kind)
    {
        return kind switch
        {
            SuggestionKind.Keyword => "🔑",
            SuggestionKind.Node => "⬡",
            SuggestionKind.Property => "◆",
            SuggestionKind.Variable => "𝑥",
            SuggestionKind.Function => "ƒ",
            _ => "•"
        };
    }
}
