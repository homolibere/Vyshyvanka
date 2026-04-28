using System.Text.Json;

namespace Vyshyvanka.Core.Models;

/// <summary>
/// Represents an audit log entry.
/// </summary>
public record AuditLog
{
    public Guid Id { get; init; }
    public DateTime Timestamp { get; init; }
    public AuditEventType EventType { get; init; }
    public Guid? UserId { get; init; }
    public string? UserEmail { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? ResourceType { get; init; }
    public Guid? ResourceId { get; init; }
    public string Action { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public JsonElement? Details { get; init; }
}

/// <summary>
/// Types of audit events.
/// </summary>
public enum AuditEventType
{
    Authentication,
    Authorization,
    WorkflowOperation,
    ExecutionOperation,
    CredentialOperation,
    ApiKeyOperation,
    UserOperation
}
