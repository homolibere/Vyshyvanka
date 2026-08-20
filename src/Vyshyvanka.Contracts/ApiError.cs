namespace Vyshyvanka.Contracts;

/// <summary>
/// Standard error payload returned by the API for any non-success response.
/// Produced by the API's error-handling middleware so that all failures share
/// a consistent shape regardless of the endpoint that raised them.
/// </summary>
public record ApiError
{
    /// <summary>
    /// Stable, machine-readable error code (e.g. <c>WORKFLOW_NOT_FOUND</c>).
    /// Intended for programmatic handling by clients; unlike <see cref="Message"/> it is not localized and does not change wording.
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Human-readable description of what went wrong, suitable for displaying to an end user.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Optional per-field validation details, keyed by field name with one or more messages per field.
    /// <c>null</c> when the error is not field-specific (e.g. a not-found or authorization failure).
    /// </summary>
    public Dictionary<string, string[]>? Details { get; init; }

    /// <summary>
    /// Correlation identifier for the failed request, used to locate the corresponding server-side logs and traces.
    /// <c>null</c> when tracing is unavailable for the request.
    /// </summary>
    public string? TraceId { get; init; }
}
