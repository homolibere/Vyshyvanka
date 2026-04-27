namespace FlowForge.Designer.Models;

/// <summary>
/// Standard API error response matching the server's ApiError format.
/// </summary>
public record ApiError
{
    /// <summary>Error code for programmatic handling.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Human-readable error message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Additional error details by field.</summary>
    public Dictionary<string, string[]>? Details { get; init; }

    /// <summary>Trace ID for debugging.</summary>
    public string? TraceId { get; init; }

    /// <summary>Gets a formatted error message including details if available.</summary>
    public string GetFullMessage()
    {
        if (Details is null || Details.Count == 0)
            return Message;

        var detailMessages = Details
            .SelectMany(kvp => kvp.Value.Select(v => $"{kvp.Key}: {v}"))
            .ToList();

        return detailMessages.Count > 0
            ? $"{Message} ({string.Join("; ", detailMessages)})"
            : Message;
    }
}

/// <summary>
/// Exception thrown when an API call returns an error response.
/// </summary>
public class ApiException : Exception
{
    /// <summary>The API error details.</summary>
    public ApiError Error { get; }

    /// <summary>The HTTP status code.</summary>
    public int StatusCode { get; }

    public ApiException(ApiError error, int statusCode)
        : base(error.Message)
    {
        Error = error;
        StatusCode = statusCode;
    }

    public ApiException(string message, int statusCode)
        : base(message)
    {
        Error = new ApiError { Code = "UNKNOWN", Message = message };
        StatusCode = statusCode;
    }
}
