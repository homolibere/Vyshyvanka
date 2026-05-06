namespace Vyshyvanka.Designer.Services;

/// <summary>
/// Proactively refreshes the access token before it expires.
/// Schedules a refresh at 80% of the token's lifetime or 1 minute before expiry,
/// whichever comes first.
/// </summary>
public sealed class TokenRefreshService(
    AuthService authService,
    AuthStateService authState,
    ILogger<TokenRefreshService> logger) : IDisposable
{
    private CancellationTokenSource? _refreshCts;
    private bool _disposed;
    private bool _initialized;

    /// <summary>
    /// Minimum remaining lifetime before we attempt a refresh (1 minute).
    /// </summary>
    private static readonly TimeSpan MinBufferBeforeExpiry = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Starts listening for auth state changes and schedules refresh if already authenticated.
    /// </summary>
    public void Start()
    {
        if (_initialized) return;
        _initialized = true;

        authState.OnAuthStateChanged += OnAuthStateChanged;
        ScheduleRefresh();
    }

    private void OnAuthStateChanged()
    {
        ScheduleRefresh();
    }

    private void ScheduleRefresh()
    {
        // Cancel any previously scheduled refresh
        CancelPendingRefresh();

        if (!authState.IsAuthenticated || authState.ExpiresAt is null)
            return;

        var now = DateTime.UtcNow;
        var expiresAt = authState.ExpiresAt.Value;
        var lifetime = expiresAt - now;

        if (lifetime <= TimeSpan.Zero)
            return;

        // Refresh at 80% of lifetime or 1 minute before expiry, whichever is sooner
        var refreshAt80Percent = TimeSpan.FromTicks((long)(lifetime.Ticks * 0.8));
        var refreshAtBuffer = lifetime - MinBufferBeforeExpiry;

        var delay = refreshAtBuffer > TimeSpan.Zero
            ? TimeSpan.FromTicks(Math.Min(refreshAt80Percent.Ticks, refreshAtBuffer.Ticks))
            : TimeSpan.FromTicks(Math.Max(lifetime.Ticks / 2, 0));

        if (delay <= TimeSpan.Zero)
        {
            // Token is about to expire imminently, refresh now
            _ = ExecuteRefreshAsync();
            return;
        }

        logger.LogDebug(
            "Token refresh scheduled in {Delay} (expires at {ExpiresAt})",
            delay,
            expiresAt);

        _refreshCts = new CancellationTokenSource();
        var ct = _refreshCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, ct);
                if (!ct.IsCancellationRequested)
                {
                    await ExecuteRefreshAsync();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when auth state changes or service is disposed
            }
        }, ct);
    }

    private async Task ExecuteRefreshAsync()
    {
        try
        {
            logger.LogDebug("Attempting proactive token refresh");
            var success = await authService.RefreshTokenAsync();

            if (success)
            {
                logger.LogDebug("Proactive token refresh succeeded");
                // OnAuthStateChanged will fire and schedule the next refresh
            }
            else
            {
                logger.LogWarning("Proactive token refresh failed — user will be logged out");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during proactive token refresh");
        }
    }

    private void CancelPendingRefresh()
    {
        if (_refreshCts is not null)
        {
            _refreshCts.Cancel();
            _refreshCts.Dispose();
            _refreshCts = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        authState.OnAuthStateChanged -= OnAuthStateChanged;
        CancelPendingRefresh();
    }
}
