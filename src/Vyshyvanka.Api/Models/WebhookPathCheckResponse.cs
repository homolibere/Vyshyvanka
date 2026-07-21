namespace Vyshyvanka.Api.Models;

/// <summary>
/// Response for webhook path availability check.
/// </summary>
public record WebhookPathCheckResponse
{
    /// <summary>Whether the webhook path is available for use.</summary>
    public bool IsAvailable { get; init; }

    /// <summary>ID of the workflow that already uses this path (if not available).</summary>
    public Guid? ConflictingWorkflowId { get; init; }

    /// <summary>Name of the workflow that already uses this path (if not available).</summary>
    public string? ConflictingWorkflowName { get; init; }
}
