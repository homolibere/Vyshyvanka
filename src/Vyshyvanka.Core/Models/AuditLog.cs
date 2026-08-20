using System.Text.Json;

namespace Vyshyvanka.Core.Models;

/// <summary>
/// Represents an audit log entry.
/// </summary>
public record AuditLog
{
    /// <summary>Unique identifier of the audit log entry.</summary>
    public Guid Id { get; init; }

    /// <summary>UTC timestamp when the audited operation occurred.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Category of the audited event.</summary>
    public AuditEventType EventType { get; init; }

    /// <summary>Identifier of the acting user. Null for anonymous or system actions.</summary>
    public Guid? UserId { get; init; }

    /// <summary>Email of the acting user, captured at event time. Null when unavailable.</summary>
    public string? UserEmail { get; init; }

    /// <summary>Source IP address of the request. Null when it could not be determined.</summary>
    public string? IpAddress { get; init; }

    /// <summary>User-agent string of the request. Null when unavailable.</summary>
    public string? UserAgent { get; init; }

    /// <summary>Type of resource affected (e.g. "Workflow", "Credential"). Null when not resource-specific.</summary>
    public string? ResourceType { get; init; }

    /// <summary>Identifier of the affected resource. Null when not applicable.</summary>
    public Guid? ResourceId { get; init; }

    /// <summary>Human-readable description of the action taken.</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>Whether the audited operation completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Error message when the operation failed. Null on success.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Optional structured JSON details captured with the event. Null when none.</summary>
    public JsonElement? Details { get; init; }
}

/// <summary>
/// Types of audit events.
/// </summary>
public enum AuditEventType
{
    /// <summary>A login or authentication attempt.</summary>
    Authentication,

    /// <summary>An authorization check on a protected resource.</summary>
    Authorization,

    /// <summary>A create, update, or delete on a workflow.</summary>
    WorkflowOperation,

    /// <summary>A trigger, cancel, or other execution-related operation.</summary>
    ExecutionOperation,

    /// <summary>A create, update, or delete on a credential.</summary>
    CredentialOperation,

    /// <summary>A create, revoke, or delete on an API key.</summary>
    ApiKeyOperation,

    /// <summary>A create, update, role change, or delete on a user.</summary>
    UserOperation
}
