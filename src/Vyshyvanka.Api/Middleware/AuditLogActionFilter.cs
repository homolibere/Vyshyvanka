using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Api.Middleware;

/// <summary>
/// Action filter that automatically logs successful mutating API operations (POST, PUT, DELETE, PATCH)
/// to the audit log. Produces human-readable action descriptions with resource context.
/// </summary>
public class AuditLogActionFilter(IAuditLogService auditLogService, ICurrentUserService currentUserService) : IAsyncActionFilter
{
    private static readonly HashSet<string> MutatingMethods = ["POST", "PUT", "DELETE", "PATCH"];

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var resultContext = await next();

        // Only log mutating operations that succeeded (2xx status)
        if (!MutatingMethods.Contains(context.HttpContext.Request.Method))
        {
            return;
        }

        var statusCode = (resultContext.Result as ObjectResult)?.StatusCode
                         ?? (resultContext.Result as StatusCodeResult)?.StatusCode
                         ?? context.HttpContext.Response.StatusCode;

        if (statusCode < 200 || statusCode >= 300)
        {
            return;
        }

        // Skip auth endpoints (already logged by AuthService) and audit-logs itself
        var path = context.HttpContext.Request.Path.Value ?? "";
        if (path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (path.StartsWith("/api/audit-logs", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var eventType = MapEventType(path);
        var resourceId = ExtractResourceId(context, resultContext);
        var action = FormatAction(context, resultContext);
        var userId = currentUserService.UserId;
        var userEmail = context.HttpContext.User.FindFirst("email")?.Value
                        ?? context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString();

        await auditLogService.LogOperationAsync(
            eventType,
            userId,
            userEmail,
            action,
            resourceType: GetResourceType(path),
            resourceId: resourceId,
            success: true,
            ipAddress: ipAddress,
            cancellationToken: context.HttpContext.RequestAborted);
    }

    private static string FormatAction(ActionExecutingContext context, ActionExecutedContext resultContext)
    {
        var method = context.HttpContext.Request.Method;
        var actionName = GetActionMethodName(context);
        var resourceName = ExtractResourceName(context, resultContext);

        // Build human-readable action
        var verb = method switch
        {
            "POST" => "Created",
            "PUT" or "PATCH" => "Updated",
            "DELETE" => "Deleted",
            _ => method
        };

        // Special cases for better readability
        var readable = actionName switch
        {
            "TriggerExecution" => $"Triggered execution{ForResource(resourceName)}",
            "Create" or "CreateUser" => $"Created{ForResource(resourceName)}",
            "Update" or "UpdateProfile" => $"Updated{ForResource(resourceName)}",
            "UpdateRole" => $"Changed role{ForResource(resourceName)}",
            "UpdateStatus" => $"Updated status{ForResource(resourceName)}",
            "Delete" => $"Deleted{ForResource(resourceName)}",
            "Install" => $"Installed package{ForResource(resourceName)}",
            "Uninstall" => $"Uninstalled package{ForResource(resourceName)}",
            "Revoke" => $"Revoked{ForResource(resourceName)}",
            _ => $"{verb}{ForResource(resourceName ?? actionName)}"
        };

        return readable;
    }

    private static string ForResource(string? name)
        => string.IsNullOrEmpty(name) ? "" : $" '{name}'";

    private static string? ExtractResourceName(ActionExecutingContext context, ActionExecutedContext resultContext)
    {
        // Try to get name from the response object (most accurate)
        if (resultContext.Result is ObjectResult { Value: not null } objResult)
        {
            var value = objResult.Value;
            var nameProperty = value.GetType().GetProperty("Name")
                               ?? value.GetType().GetProperty("WorkflowName");
            if (nameProperty?.GetValue(value) is string name)
            {
                return name;
            }
        }

        // Try to get name/workflowId from action arguments (request body)
        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg is null)
            {
                continue;
            }

            var type = arg.GetType();

            // Check for Name property on the request
            var nameProp = type.GetProperty("Name");
            if (nameProp?.GetValue(arg) is string reqName)
            {
                return reqName;
            }
        }

        return null;
    }

    private static Guid? ExtractResourceId(ActionExecutingContext context, ActionExecutedContext resultContext)
    {
        // Try from route parameters
        if (context.ActionArguments.TryGetValue("id", out var idObj) && idObj is Guid id)
        {
            return id;
        }

        // Try WorkflowId from request body (e.g., TriggerExecutionRequest)
        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg is null)
            {
                continue;
            }

            var workflowIdProp = arg.GetType().GetProperty("WorkflowId");
            if (workflowIdProp?.GetValue(arg) is Guid wfId)
            {
                return wfId;
            }
        }

        // Try Id from response
        if (resultContext.Result is ObjectResult { Value: not null } objResult)
        {
            var idProp = objResult.Value.GetType().GetProperty("Id");
            if (idProp?.GetValue(objResult.Value) is Guid resultId)
            {
                return resultId;
            }
        }

        return null;
    }

    private static string GetActionMethodName(ActionExecutingContext context)
    {
        var displayName = context.ActionDescriptor.DisplayName ?? "";
        if (displayName.Contains('('))
        {
            return displayName[..displayName.IndexOf('(')].Split('.').Last().Trim();
        }

        return displayName.Trim();
    }

    private static AuditEventType MapEventType(string path) => path.ToLowerInvariant() switch
    {
        var p when p.StartsWith("/api/workflow") => AuditEventType.WorkflowOperation,
        var p when p.StartsWith("/api/execution") => AuditEventType.ExecutionOperation,
        var p when p.StartsWith("/api/credential") => AuditEventType.CredentialOperation,
        var p when p.StartsWith("/api/apikey") => AuditEventType.ApiKeyOperation,
        var p when p.StartsWith("/api/user") => AuditEventType.UserOperation,
        var p when p.StartsWith("/api/team") => AuditEventType.UserOperation,
        var p when p.StartsWith("/api/folder") => AuditEventType.WorkflowOperation,
        var p when p.StartsWith("/api/package") => AuditEventType.WorkflowOperation,
        var p when p.StartsWith("/api/webhook") => AuditEventType.ExecutionOperation,
        _ => AuditEventType.WorkflowOperation
    };

    private static string GetResourceType(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 ? segments[1] : "unknown";
    }
}
