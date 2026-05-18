namespace Vyshyvanka.Designer.Services;

/// <summary>
/// Lightweight service that holds the current drag-and-drop expression data.
/// Used to pass expression text from the JSON viewer to property editors
/// without requiring JavaScript interop for dataTransfer.
/// </summary>
public sealed class ExpressionDragService
{
    /// <summary>
    /// The expression text currently being dragged (e.g., "{{ input.item.projectId }}").
    /// Null when no drag is in progress.
    /// </summary>
    public string? CurrentExpression { get; private set; }

    /// <summary>
    /// Whether a drag operation is currently active.
    /// </summary>
    public bool IsDragging => CurrentExpression is not null;

    /// <summary>
    /// Called when a drag operation starts from the JSON viewer.
    /// </summary>
    public void StartDrag(string expression)
    {
        CurrentExpression = expression;
    }

    /// <summary>
    /// Called when the drag operation ends (drop or cancel).
    /// </summary>
    public void EndDrag()
    {
        CurrentExpression = null;
    }
}
