using System.Text.Json;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Engine.Persistence;
using Vyshyvanka.Engine.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Vyshyvanka.Engine.Auth;

/// <summary>
/// Service for audit logging.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly VyshyvankaDbContext _context;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(VyshyvankaDbContext context, ILogger<AuditLogService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogAuthenticationAttemptAsync(
        string email,
        bool success,
        string? ipAddress = null,
        string? userAgent = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var action = success ? "Login" : "LoginFailed";
        
        await LogOperationAsync(
            AuditEventType.Authentication,
            null,
            email,
            action,
            "User",
            null,
            success,
            ipAddress,
            userAgent,
            errorMessage,
            null,
            cancellationToken);
        
        if (success)
        {
            _logger.LogInformation("User {Email} logged in from {IpAddress}", email, ipAddress);
        }
        else
        {
            _logger.LogWarning("Failed login attempt for {Email} from {IpAddress}: {Error}", email, ipAddress, errorMessage);
        }
    }

    public async Task LogAuthorizationAttemptAsync(
        Guid? userId,
        string? userEmail,
        string action,
        string? resourceType,
        Guid? resourceId,
        bool success,
        string? ipAddress = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        await LogOperationAsync(
            AuditEventType.Authorization,
            userId,
            userEmail,
            action,
            resourceType,
            resourceId,
            success,
            ipAddress,
            null,
            errorMessage,
            null,
            cancellationToken);
        
        if (!success)
        {
            _logger.LogWarning(
                "Authorization denied for user {UserId} ({Email}) attempting {Action} on {ResourceType}/{ResourceId}: {Error}",
                userId, userEmail, action, resourceType, resourceId, errorMessage);
        }
    }

    public async Task LogOperationAsync(
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
        CancellationToken cancellationToken = default)
    {
        var entity = new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            UserId = userId,
            UserEmail = userEmail,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Action = action,
            Success = success,
            ErrorMessage = errorMessage,
            DetailsJson = details?.GetRawText()
        };

        _context.AuditLogs.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<AuditLog>> GetLogsAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var queryable = _context.AuditLogs.AsNoTracking();

        if (query.UserId.HasValue)
        {
            queryable = queryable.Where(l => l.UserId == query.UserId.Value);
        }

        if (query.EventType.HasValue)
        {
            queryable = queryable.Where(l => l.EventType == query.EventType.Value);
        }

        if (!string.IsNullOrEmpty(query.ResourceType))
        {
            queryable = queryable.Where(l => l.ResourceType == query.ResourceType);
        }

        if (query.ResourceId.HasValue)
        {
            queryable = queryable.Where(l => l.ResourceId == query.ResourceId.Value);
        }

        if (query.FromDate.HasValue)
        {
            queryable = queryable.Where(l => l.Timestamp >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            queryable = queryable.Where(l => l.Timestamp <= query.ToDate.Value);
        }

        if (query.Success.HasValue)
        {
            queryable = queryable.Where(l => l.Success == query.Success.Value);
        }

        var entities = await queryable
            .OrderByDescending(l => l.Timestamp)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(cancellationToken);

        return entities.Select(ToModel);
    }

    private static AuditLog ToModel(AuditLogEntity entity) => new AuditLog
    {
        Id = entity.Id,
        Timestamp = entity.Timestamp,
        EventType = entity.EventType,
        UserId = entity.UserId,
        UserEmail = entity.UserEmail,
        IpAddress = entity.IpAddress,
        UserAgent = entity.UserAgent,
        ResourceType = entity.ResourceType,
        ResourceId = entity.ResourceId,
        Action = entity.Action,
        Success = entity.Success,
        ErrorMessage = entity.ErrorMessage,
        Details = string.IsNullOrEmpty(entity.DetailsJson) 
            ? null 
            : JsonSerializer.Deserialize<JsonElement>(entity.DetailsJson)
    };
}
