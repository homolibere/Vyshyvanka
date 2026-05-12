using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Vyshyvanka.Api.Extensions;

/// <summary>
/// Extension methods for configuring rate limiting policies.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Policy name for authentication endpoints (login, register, refresh).
    /// 5 requests per minute per IP.
    /// </summary>
    public const string AuthPolicy = "auth";

    /// <summary>
    /// Policy name for webhook trigger endpoints.
    /// 30 requests per minute per IP.
    /// </summary>
    public const string WebhookPolicy = "webhook";

    /// <summary>
    /// Policy name applied globally to all other endpoints.
    /// 100 requests per minute per IP.
    /// </summary>
    public const string GeneralPolicy = "general";

    /// <summary>
    /// Adds rate limiting services with auth, webhook, and general policies.
    /// </summary>
    public static IServiceCollection AddVyshyvankaRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/json";

                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue)
                    ? retryAfterValue
                    : TimeSpan.FromMinutes(1);

                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)retryAfter.TotalSeconds).ToString();

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    code = "RATE_LIMITED",
                    message = "Too many requests. Please try again later.",
                    retryAfterSeconds = (int)retryAfter.TotalSeconds
                }, cancellationToken);
            };

            // Auth endpoints: strict — 5 requests per minute per IP
            options.AddFixedWindowLimiter(AuthPolicy, limiter =>
            {
                limiter.PermitLimit = 5;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiter.QueueLimit = 0;
            });

            // Webhook endpoints: moderate — 30 requests per minute per IP
            options.AddFixedWindowLimiter(WebhookPolicy, limiter =>
            {
                limiter.PermitLimit = 30;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiter.QueueLimit = 0;
            });

            // General: 100 requests per minute per IP (global fallback)
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));
        });

        return services;
    }
}
