using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Engine.Persistence.Entities;

/// <summary>
/// EF Core entity for audit log storage.
/// </summary>
public class AuditLogEntity
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }
    
    /// <summary>Timestamp of the event.</summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>Type of audit event.</summary>
    public AuditEventType EventType { get; set; }
    
    /// <summary>User ID (if authenticated).</summary>
    public Guid? UserId { get; set; }
    
    /// <summary>User email (if available).</summary>
    public string? UserEmail { get; set; }
    
    /// <summary>Client IP address.</summary>
    public string? IpAddress { get; set; }
    
    /// <summary>Client user agent.</summary>
    public string? UserAgent { get; set; }
    
    /// <summary>Type of resource being accessed.</summary>
    public string? ResourceType { get; set; }
    
    /// <summary>ID of the resource being accessed.</summary>
    public Guid? ResourceId { get; set; }
    
    /// <summary>Action being performed.</summary>
    public string Action { get; set; } = string.Empty;
    
    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; set; }
    
    /// <summary>Error message if operation failed.</summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>Additional details as JSON.</summary>
    public string? DetailsJson { get; set; }
}
