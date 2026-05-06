using System.Text.Json;
using Vyshyvanka.Designer.Models;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// Singleton service that holds authentication state.
/// Persists tokens to browser localStorage to survive page refreshes.
/// </summary>
public class AuthStateService
{
    private const string StorageKey = "vyshyvanka_auth";

    private BrowserStorageService? _storage;
    private string? _accessToken;
    private string? _refreshToken;
    private DateTime? _expiresAt;
    private UserInfo? _currentUser;
    private bool _initialized;

    /// <summary>Gets whether the user is currently authenticated.</summary>
    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken) && _expiresAt > DateTime.UtcNow;

    /// <summary>Gets the current user info.</summary>
    public UserInfo? CurrentUser => _currentUser;

    /// <summary>Gets the current access token.</summary>
    public string? AccessToken => _accessToken;

    /// <summary>Gets the current refresh token.</summary>
    public string? RefreshToken => _refreshToken;

    /// <summary>Gets the token expiration time (UTC).</summary>
    public DateTime? ExpiresAt => _expiresAt;

    /// <summary>Event raised when authentication state changes.</summary>
    public event Action? OnAuthStateChanged;

    /// <summary>
    /// Initializes the storage service. Must be called after JS runtime is available.
    /// </summary>
    public void SetStorageService(BrowserStorageService storage)
    {
        _storage = storage;
    }

    /// <summary>
    /// Loads auth state from browser storage. Call on app startup.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized || _storage is null) return;
        _initialized = true;

        try
        {
            var json = await _storage.GetItemAsync(StorageKey);
            if (string.IsNullOrEmpty(json)) return;

            var stored = JsonSerializer.Deserialize<StoredAuthState>(json);
            if (stored is null) return;

            // Check if token is still valid
            if (stored.ExpiresAt > DateTime.UtcNow)
            {
                _accessToken = stored.AccessToken;
                _refreshToken = stored.RefreshToken;
                _expiresAt = stored.ExpiresAt;
                _currentUser = stored.User;
                OnAuthStateChanged?.Invoke();
            }
            else
            {
                // Token expired, clear storage
                await _storage.RemoveItemAsync(StorageKey);
            }
        }
        catch
        {
            // Ignore storage errors
        }
    }

    /// <summary>
    /// Sets the authentication state from a login response.
    /// </summary>
    public async Task SetAuthStateAsync(string accessToken, string refreshToken, DateTime expiresAt, UserInfo user)
    {
        _accessToken = accessToken;
        _refreshToken = refreshToken;
        _expiresAt = expiresAt;
        _currentUser = user;

        await PersistToStorageAsync();
        OnAuthStateChanged?.Invoke();
    }

    /// <summary>
    /// Sets the authentication state (sync version for compatibility).
    /// </summary>
    public void SetAuthState(string accessToken, string refreshToken, DateTime expiresAt, UserInfo user)
    {
        _accessToken = accessToken;
        _refreshToken = refreshToken;
        _expiresAt = expiresAt;
        _currentUser = user;

        _ = PersistToStorageAsync();
        OnAuthStateChanged?.Invoke();
    }

    /// <summary>
    /// Clears the authentication state.
    /// </summary>
    public async Task ClearAuthStateAsync()
    {
        _accessToken = null;
        _refreshToken = null;
        _expiresAt = null;
        _currentUser = null;

        if (_storage is not null)
        {
            await _storage.RemoveItemAsync(StorageKey);
        }

        OnAuthStateChanged?.Invoke();
    }

    /// <summary>
    /// Clears the authentication state (sync version for compatibility).
    /// </summary>
    public void ClearAuthState()
    {
        _accessToken = null;
        _refreshToken = null;
        _expiresAt = null;
        _currentUser = null;

        if (_storage is not null)
        {
            _ = _storage.RemoveItemAsync(StorageKey);
        }

        OnAuthStateChanged?.Invoke();
    }

    private async Task PersistToStorageAsync()
    {
        if (_storage is null || _accessToken is null) return;

        var state = new StoredAuthState
        {
            AccessToken = _accessToken,
            RefreshToken = _refreshToken,
            ExpiresAt = _expiresAt,
            User = _currentUser
        };

        var json = JsonSerializer.Serialize(state);
        await _storage.SetItemAsync(StorageKey, json);
    }

    private record StoredAuthState
    {
        public string? AccessToken { get; init; }
        public string? RefreshToken { get; init; }
        public DateTime? ExpiresAt { get; init; }
        public UserInfo? User { get; init; }
    }
}
