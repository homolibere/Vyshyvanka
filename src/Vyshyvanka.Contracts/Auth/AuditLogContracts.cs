namespace Vyshyvanka.Contracts.Auth;

/// <summary>
/// Response record for a single audit log entry.
/// </summary>
public record AuditLogResponse
{
    public Guid Id { get; init; }
    public DateTime Timestamp { get; init; }
    public string EventType { get; init; } = string.Empty;
    public Guid? UserId { get; init; }
    public string? UserEmail { get; init; }
    public string? IpAddress { get; init; }
    public string? ResourceType { get; init; }
    public Guid? ResourceId { get; init; }
    public string Action { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Paginated list of audit log entries.
/// </summary>
public record AuditLogListResponse
{
    public List<AuditLogResponse> Logs { get; init; } = [];
    public int TotalCount { get; init; }
}
