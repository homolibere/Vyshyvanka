namespace Vyshyvanka.Core.Interfaces;

/// <summary>
/// Writes a synchronous HTTP response back to the webhook caller.
/// Used by the HTTP Response node to return computed responses
/// when a workflow is triggered via webhook in synchronous mode.
/// </summary>
public interface IWebhookResponseWriter
{
    /// <summary>
    /// Whether a response has already been sent. Only one response per webhook invocation is allowed.
    /// </summary>
    bool IsResponseSent { get; }

    /// <summary>
    /// Writes the HTTP response to the held webhook connection.
    /// </summary>
    /// <param name="statusCode">HTTP status code to return.</param>
    /// <param name="headers">Response headers (Content-Type is set automatically if not provided).</param>
    /// <param name="body">Response body as a string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if a response has already been sent.</exception>
    Task WriteAsync(int statusCode, IReadOnlyDictionary<string, string>? headers, string? body, CancellationToken cancellationToken);
}
