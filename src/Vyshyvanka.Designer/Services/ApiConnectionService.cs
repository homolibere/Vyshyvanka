namespace Vyshyvanka.Designer.Services;

/// <summary>
/// Manages the API server connection state, URL resolution from localStorage,
/// and health-check logic. Shows a connection overlay when the API is unreachable.
/// </summary>
public sealed class ApiConnectionService : IDisposable
{
    private readonly HttpClient _appHttpClient;
    private readonly BrowserStorageService _storage;
    private readonly string _configuredUrl;
    private HttpClient? _probeClient;
    private Timer? _retryTimer;
    private int _retryDelaySeconds = 5;

    private const string StorageKey = "vyshyvanka-api-url";
    private const int MaxRetryDelay = 30;

    public ApiConnectionService(HttpClient appHttpClient, BrowserStorageService storage, string configuredUrl)
    {
        _appHttpClient = appHttpClient;
        _storage = storage;
        _configuredUrl = configuredUrl;
        CurrentUrl = configuredUrl;
    }

    /// <summary>
    /// Current connection state.
    /// </summary>
    public ApiConnectionState State { get; private set; } = ApiConnectionState.Connecting;

    /// <summary>
    /// Current API URL being used.
    /// </summary>
    public string CurrentUrl { get; private set; }

    /// <summary>
    /// Error message from the last failed connection attempt.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Seconds until the next auto-retry attempt.
    /// </summary>
    public int RetryCountdown { get; private set; }

    /// <summary>
    /// Fired when any state property changes.
    /// </summary>
    public event Action? StateChanged;

    /// <summary>
    /// Initializes the service: reads saved URL from localStorage, sets HttpClient base,
    /// and performs the first health check.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Try localStorage first, fall back to configured URL
        var savedUrl = await _storage.GetItemAsync(StorageKey);
        if (!string.IsNullOrWhiteSpace(savedUrl))
        {
            CurrentUrl = savedUrl;
        }

        await CheckConnectionAsync();
    }

    /// <summary>
    /// Attempts to connect to the given URL. On success, saves to localStorage and
    /// updates the app's HttpClient base address.
    /// </summary>
    public async Task ConnectAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        url = url.TrimEnd('/');
        CurrentUrl = url;
        StopAutoRetry();
        _retryDelaySeconds = 5;

        await CheckConnectionAsync();

        if (State == ApiConnectionState.Connected)
        {
            await _storage.SetItemAsync(StorageKey, url);
        }
    }

    /// <summary>
    /// Manually trigger a retry with the current URL.
    /// </summary>
    public async Task RetryAsync()
    {
        StopAutoRetry();
        _retryDelaySeconds = 5;
        await CheckConnectionAsync();
    }

    /// <summary>
    /// Performs a health check against GET /api/auth/config using a dedicated probe client.
    /// On success, applies the URL to the app's HttpClient.
    /// </summary>
    private async Task CheckConnectionAsync()
    {
        State = ApiConnectionState.Connecting;
        ErrorMessage = null;
        NotifyStateChanged();

        try
        {
            // Use a dedicated client for probing so we don't lock the app HttpClient's BaseAddress
            var probeUrl = CurrentUrl.TrimEnd('/') + "/api/auth/config";
            _probeClient?.Dispose();
            _probeClient = new HttpClient();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _probeClient.GetAsync(probeUrl, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                // Connection successful — apply the URL to the app's HttpClient
                ApplyUrlToAppClient(CurrentUrl);
                State = ApiConnectionState.Connected;
                ErrorMessage = null;
                StopAutoRetry();
                NotifyStateChanged();
                return;
            }

            ErrorMessage = $"Server returned {(int)response.StatusCode} {response.ReasonPhrase}";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "Connection timed out";
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = ex.InnerException?.Message ?? ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        State = ApiConnectionState.Disconnected;
        NotifyStateChanged();
        StartAutoRetry();
    }

    private void ApplyUrlToAppClient(string url)
    {
        if (Uri.TryCreate(url.TrimEnd('/') + "/", UriKind.Absolute, out var uri))
        {
            // BaseAddress can only be set before the first request on this HttpClient.
            // If it's already set to a different value and a request was made, this will throw.
            // In our flow, we block the UI until connected, so no other request should have been made.
            try
            {
                _appHttpClient.BaseAddress = uri;
            }
            catch (InvalidOperationException)
            {
                // BaseAddress was already set and a request was made through this client.
                // This shouldn't happen in normal flow since the overlay blocks all UI.
                // The URL was already matching or will match on next app restart.
            }
        }
    }

    private void StartAutoRetry()
    {
        StopAutoRetry();
        RetryCountdown = _retryDelaySeconds;
        NotifyStateChanged();

        _retryTimer = new Timer(async _ =>
        {
            RetryCountdown--;
            if (RetryCountdown <= 0)
            {
                StopAutoRetry();
                await CheckConnectionAsync();
                // Exponential backoff
                _retryDelaySeconds = Math.Min(_retryDelaySeconds * 2, MaxRetryDelay);
            }
            else
            {
                NotifyStateChanged();
            }
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private void StopAutoRetry()
    {
        _retryTimer?.Dispose();
        _retryTimer = null;
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();

    public void Dispose()
    {
        StopAutoRetry();
        _probeClient?.Dispose();
    }
}

public enum ApiConnectionState
{
    Connecting,
    Connected,
    Disconnected
}
