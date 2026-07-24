using Vyshyvanka.Contracts;

namespace Vyshyvanka.Designer.Models;

/// <summary>
/// Extension methods for ApiError.
/// </summary>
public static class ApiErrorExtensions
{
    /// <summary>Gets a formatted error message including details if available.</summary>
    public static string GetFullMessage(this ApiError error)
    {
        if (error.Details is null || error.Details.Count == 0)
            return error.Message;

        var detailMessages = error.Details
            .SelectMany(kvp => kvp.Value.Select(v => $"{kvp.Key}: {v}"))
            .ToList();

        return detailMessages.Count > 0
            ? $"{error.Message} ({string.Join("; ", detailMessages)})"
            : error.Message;
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
