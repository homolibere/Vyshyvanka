using Vyshyvanka.Api.Authorization;
using Vyshyvanka.Contracts;
using Vyshyvanka.Contracts.Auth;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Vyshyvanka.Api.Controllers;

/// <summary>
/// Admin endpoints for viewing audit logs.
/// </summary>
[ApiController]
[Route("api/audit-logs")]
[Produces("application/json")]
[Authorize(Policy = Policies.CanManageUsers)]
public class AuditLogController(IAuditLogService auditLogService) : ControllerBase
{
    /// <summary>
    /// Lists audit log entries with optional filtering and pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(AuditLogListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuditLogListResponse>> GetLogs(
        [FromQuery] string? eventType = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? resourceType = null,
        [FromQuery] bool? success = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);
        skip = Math.Max(0, skip);

        AuditEventType? parsedEventType = null;
        if (!string.IsNullOrEmpty(eventType) &&
            Enum.TryParse<AuditEventType>(eventType, ignoreCase: true, out var et))
        {
            parsedEventType = et;
        }

        var query = new AuditLogQuery
        {
            UserId = userId,
            EventType = parsedEventType,
            ResourceType = resourceType,
            FromDate = fromDate,
            ToDate = toDate,
            Success = success,
            Skip = skip,
            Take = take
        };

        var logs = await auditLogService.GetLogsAsync(query, cancellationToken);
        var items = logs.Select(l => new AuditLogResponse
        {
            Id = l.Id,
            Timestamp = l.Timestamp,
            EventType = l.EventType.ToString(),
            UserId = l.UserId,
            UserEmail = l.UserEmail,
            IpAddress = l.IpAddress,
            ResourceType = l.ResourceType,
            ResourceId = l.ResourceId,
            Action = l.Action,
            Success = l.Success,
            ErrorMessage = l.ErrorMessage
        }).ToList();

        return Ok(new AuditLogListResponse
        {
            Logs = items,
            TotalCount = items.Count
        });
    }
}
