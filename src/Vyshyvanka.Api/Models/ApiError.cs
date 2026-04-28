namespace Vyshyvanka.Api.Models;

/// <summary>
/// Standard API error response.
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
}
