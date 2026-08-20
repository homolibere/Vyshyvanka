using System.Text.Json;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Core.Interfaces;

/// <summary>
/// Service for audit logging.
/// </summary>
public interface IAuditLogService
{
    /// <summary>Records an authentication attempt (login success or failure).</summary>
    Task LogAuthenticationAttemptAsync(
        string email,
        bool success,
        string? ipAddress = null,
        string? userAgent = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    /// <summary>Records an authorization attempt against a protected resource.</summary>
    Task LogAuthorizationAttemptAsync(
        Guid? userId,
        string? userEmail,
        string action,
        string? resourceType,
        Guid? resourceId,
        bool success,
        string? ipAddress = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    /// <summary>Records a general auditable operation (CRUD, trigger, etc.).</summary>
    Task LogOperationAsync(
        AuditEventType eventType,
        Guid? userId,
        string? userEmail,
        string action,
        string? resourceType = null,
        Guid? resourceId = null,
        bool success = true,
        string? ipAddress = null,
        string? userAgent = null,
        string? errorMessage = null,
        JsonElement? details = null,
        CancellationToken cancellationToken = default);

    /// <summary>Queries audit log entries matching the specified criteria.</summary>
    Task<IEnumerable<AuditLog>> GetLogsAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Query parameters for audit logs.
/// </summary>
public record AuditLogQuery
{
    /// <summary>Filter to entries by a specific user. Null returns all users.</summary>
    public Guid? UserId { get; init; }

    /// <summary>Filter by event type. Null returns all types.</summary>
    public AuditEventType? EventType { get; init; }

    /// <summary>Filter by resource type (e.g. "Workflow"). Null returns all resource types.</summary>
    public string? ResourceType { get; init; }

    /// <summary>Filter by resource identifier. Null returns all resources.</summary>
    public Guid? ResourceId { get; init; }

    /// <summary>Inclusive lower bound on the event timestamp (UTC). Null for no lower bound.</summary>
    public DateTime? FromDate { get; init; }

    /// <summary>Inclusive upper bound on the event timestamp (UTC). Null for no upper bound.</summary>
    public DateTime? ToDate { get; init; }

    /// <summary>Filter by success/failure. Null returns both.</summary>
    public bool? Success { get; init; }

    /// <summary>Number of entries to skip before the returned page.</summary>
    public int Skip { get; init; }

    /// <summary>Maximum number of entries to return. Defaults to 50.</summary>
    public int Take { get; init; } = 50;
}
