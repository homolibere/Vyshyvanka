namespace Vyshyvanka.Contracts.Auth;

/// <summary>
/// A single audit log entry recording a security-relevant or mutating operation.
/// </summary>
public record AuditLogResponse
{
    /// <summary>Unique identifier of the audit log entry.</summary>
    public Guid Id { get; init; }

    /// <summary>UTC timestamp when the audited operation occurred.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Category of the event (e.g. <c>WorkflowOperation</c>, <c>CredentialOperation</c>).</summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>Identifier of the user who performed the operation. <c>null</c> for anonymous or system actions.</summary>
    public Guid? UserId { get; init; }

    /// <summary>Email of the acting user, captured at the time of the event. <c>null</c> when unavailable.</summary>
    public string? UserEmail { get; init; }

    /// <summary>Source IP address of the request. <c>null</c> when it could not be determined.</summary>
    public string? IpAddress { get; init; }

    /// <summary>Type of resource affected (e.g. <c>Workflow</c>, <c>Credential</c>). <c>null</c> when not resource-specific.</summary>
    public string? ResourceType { get; init; }

    /// <summary>Identifier of the affected resource. <c>null</c> when not applicable.</summary>
    public Guid? ResourceId { get; init; }

    /// <summary>Human-readable description of the action taken (e.g. <c>Triggered execution "New Workflow"</c>).</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>Whether the operation completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Error message when the operation failed. <c>null</c> on success.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Paginated collection of audit log entries.
/// </summary>
public record AuditLogListResponse
{
    /// <summary>The audit log entries on the current page.</summary>
    public List<AuditLogResponse> Logs { get; init; } = [];

    /// <summary>Total number of entries matching the query across all pages.</summary>
    public int TotalCount { get; init; }
}
