using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Engine.Execution;

/// <summary>
/// Holds a pending webhook HTTP response until the HTTP Response node writes to it,
/// or until the timeout expires. The WebhookController awaits <see cref="WaitForResponseAsync"/>
/// while the workflow engine executes nodes in parallel.
/// </summary>
public sealed class WebhookResponseWriter : IWebhookResponseWriter
{
    private readonly TaskCompletionSource<WebhookResponseData> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _responseSent;

    /// <inheritdoc />
    public bool IsResponseSent => Volatile.Read(ref _responseSent) == 1;

    /// <inheritdoc />
    public Task WriteAsync(int statusCode, IReadOnlyDictionary<string, string>? headers, string? body, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.CompareExchange(ref _responseSent, 1, 0) != 0)
        {
            throw new InvalidOperationException("A webhook response has already been sent for this execution. Only one HTTP Response node can fire per workflow.");
        }

        var data = new WebhookResponseData(statusCode, headers, body);
        _tcs.TrySetResult(data);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Waits for the HTTP Response node to write a response, or returns null on timeout.
    /// Called by the WebhookController to hold the connection open.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for the workflow to produce a response.</param>
    /// <param name="cancellationToken">Cancellation token (e.g., client disconnect).</param>
    /// <returns>The response data, or null if the timeout elapsed without a response.</returns>
    public async Task<WebhookResponseData?> WaitForResponseAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await _tcs.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout elapsed, not a client cancellation
            return null;
        }
    }

    /// <summary>
    /// Marks the response as failed (e.g., workflow threw before reaching HTTP Response node).
    /// This unblocks the WaitForResponseAsync caller.
    /// </summary>
    /// <param name="errorMessage">Error description.</param>
    public void SetFailed(string errorMessage)
    {
        Interlocked.CompareExchange(ref _responseSent, 1, 0);
        _tcs.TrySetResult(new WebhookResponseData(500, null, errorMessage));
    }
}

/// <summary>
/// Data written by the HTTP Response node and consumed by the WebhookController.
/// </summary>
/// <param name="StatusCode">HTTP status code.</param>
/// <param name="Headers">Optional response headers.</param>
/// <param name="Body">Response body string (typically JSON).</param>
public sealed record WebhookResponseData(
    int StatusCode,
    IReadOnlyDictionary<string, string>? Headers,
    string? Body);
