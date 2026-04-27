using System.Text.Json;
using FlowForge.Core.Models;

namespace FlowForge.Core.Interfaces;

/// <summary>
/// Service for audit logging.
/// </summary>
public interface IAuditLogService
{
    Task LogAuthenticationAttemptAsync(
        string email,
        bool success,
        string? ipAddress = null,
        string? userAgent = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

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

    Task<IEnumerable<AuditLog>> GetLogsAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Query parameters for audit logs.
/// </summary>
public record AuditLogQuery
{
    public Guid? UserId { get; init; }
    public AuditEventType? EventType { get; init; }
    public string? ResourceType { get; init; }
    public Guid? ResourceId { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public bool? Success { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; } = 50;
}
