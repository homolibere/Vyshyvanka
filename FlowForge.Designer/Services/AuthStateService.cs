using FlowForge.Designer.Models;

namespace FlowForge.Designer.Services;

/// <summary>
/// Singleton service that holds authentication state.
/// Separated from AuthService to avoid scoped-in-singleton DI issues.
/// </summary>
public class AuthStateService
{
    private string? _accessToken;
    private string? _refreshToken;
    private DateTime? _expiresAt;
    private UserInfo? _currentUser;

    /// <summary>Gets whether the user is currently authenticated.</summary>
    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken) && _expiresAt > DateTime.UtcNow;

    /// <summary>Gets the current user info.</summary>
    public UserInfo? CurrentUser => _currentUser;

    /// <summary>Gets the current access token.</summary>
    public string? AccessToken => _accessToken;

    /// <summary>Gets the current refresh token.</summary>
    public string? RefreshToken => _refreshToken;

    /// <summary>Event raised when authentication state changes.</summary>
    public event Action? OnAuthStateChanged;

    /// <summary>
    /// Sets the authentication state from a login response.
    /// </summary>
    public void SetAuthState(string accessToken, string refreshToken, DateTime expiresAt, UserInfo user)
    {
        _accessToken = accessToken;
        _refreshToken = refreshToken;
        _expiresAt = expiresAt;
        _currentUser = user;
        OnAuthStateChanged?.Invoke();
    }

    /// <summary>
    /// Clears the authentication state.
    /// </summary>
    public void ClearAuthState()
    {
        _accessToken = null;
        _refreshToken = null;
        _expiresAt = null;
        _currentUser = null;
        OnAuthStateChanged?.Invoke();
    }
}
