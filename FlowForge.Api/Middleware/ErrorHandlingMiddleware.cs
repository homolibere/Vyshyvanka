using System.Net;
using System.Text.Json;
using FlowForge.Api.Models;
using FlowForge.Core.Exceptions;

namespace FlowForge.Api.Middleware;

/// <summary>
/// Middleware for handling unhandled exceptions and returning consistent API error responses.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;
        
        _logger.LogError(
            exception,
            "Unhandled exception occurred. TraceId: {TraceId}, Path: {Path}",
            traceId,
            context.Request.Path);

        var (statusCode, error) = MapExceptionToError(exception, traceId);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var json = JsonSerializer.Serialize(error, JsonOptions);
        await context.Response.WriteAsync(json);
    }

    private static (HttpStatusCode StatusCode, ApiError Error) MapExceptionToError(
        Exception exception, 
        string traceId)
    {
        return exception switch
        {
            // FlowForge-specific exceptions
            WorkflowNotFoundException ex => (
                HttpStatusCode.NotFound,
                new ApiError
                {
                    Code = ex.ErrorCode,
                    Message = ex.Message,
                    TraceId = traceId
                }),

            ExecutionNotFoundException ex => (
                HttpStatusCode.NotFound,
                new ApiError
                {
                    Code = ex.ErrorCode,
                    Message = ex.Message,
                    TraceId = traceId
                }),

            CredentialNotFoundException ex => (
                HttpStatusCode.NotFound,
                new ApiError
                {
                    Code = ex.ErrorCode,
                    Message = ex.Message,
                    TraceId = traceId
                }),

            WorkflowValidationException ex => (
                HttpStatusCode.BadRequest,
                new ApiError
                {
                    Code = ex.ErrorCode,
                    Message = ex.Message,
                    Details = new Dictionary<string, string[]>
                    {
                        ["validationErrors"] = ex.ValidationErrors.ToArray()
                    },
                    TraceId = traceId
                }),

            VersionConflictException ex => (
                HttpStatusCode.Conflict,
                new ApiError
                {
                    Code = ex.ErrorCode,
                    Message = ex.Message,
                    TraceId = traceId
                }),

            WorkflowExecutionException ex => (
                HttpStatusCode.InternalServerError,
                new ApiError
                {
                    Code = ex.ErrorCode,
                    Message = ex.Message,
                    TraceId = traceId
                }),

            // Standard exceptions
            ArgumentNullException ex => (
                HttpStatusCode.BadRequest,
                new ApiError
                {
                    Code = "INVALID_ARGUMENT",
                    Message = $"Required argument is missing: {ex.ParamName}",
                    TraceId = traceId
                }),

            ArgumentException ex => (
                HttpStatusCode.BadRequest,
                new ApiError
                {
                    Code = "INVALID_ARGUMENT",
                    Message = ex.Message,
                    TraceId = traceId
                }),

            InvalidOperationException ex => (
                HttpStatusCode.Conflict,
                new ApiError
                {
                    Code = "INVALID_OPERATION",
                    Message = ex.Message,
                    TraceId = traceId
                }),

            KeyNotFoundException => (
                HttpStatusCode.NotFound,
                new ApiError
                {
                    Code = "RESOURCE_NOT_FOUND",
                    Message = "The requested resource was not found",
                    TraceId = traceId
                }),

            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                new ApiError
                {
                    Code = "UNAUTHORIZED",
                    Message = "Authentication is required to access this resource",
                    TraceId = traceId
                }),

            NotSupportedException ex => (
                HttpStatusCode.BadRequest,
                new ApiError
                {
                    Code = "NOT_SUPPORTED",
                    Message = ex.Message,
                    TraceId = traceId
                }),

            TimeoutException => (
                HttpStatusCode.GatewayTimeout,
                new ApiError
                {
                    Code = "TIMEOUT",
                    Message = "The operation timed out",
                    TraceId = traceId
                }),

            OperationCanceledException => (
                HttpStatusCode.BadRequest,
                new ApiError
                {
                    Code = "OPERATION_CANCELLED",
                    Message = "The operation was cancelled",
                    TraceId = traceId
                }),

            JsonException ex => (
                HttpStatusCode.BadRequest,
                new ApiError
                {
                    Code = "INVALID_JSON",
                    Message = $"Invalid JSON format: {ex.Message}",
                    TraceId = traceId
                }),

            _ => (
                HttpStatusCode.InternalServerError,
                new ApiError
                {
                    Code = "INTERNAL_ERROR",
                    Message = "An unexpected error occurred. Please try again later.",
                    TraceId = traceId
                })
        };
    }
}

/// <summary>
/// Extension methods for registering the error handling middleware.
/// </summary>
public static class ErrorHandlingMiddlewareExtensions
{
    /// <summary>
    /// Adds the error handling middleware to the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ErrorHandlingMiddleware>();
    }
}
