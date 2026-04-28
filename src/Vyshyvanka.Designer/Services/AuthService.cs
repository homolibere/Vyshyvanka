using System.Net.Http.Json;
using Vyshyvanka.Designer.Models;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// Handles authentication HTTP operations. Scoped service that depends on HttpClient.
/// Auth state is stored in AuthStateService (singleton) to persist across navigation.
/// </summary>
public class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly AuthStateService _authState;

    public AuthService(HttpClient httpClient, AuthStateService authState)
    {
        _httpClient = httpClient;
        _authState = authState;
    }

    /// <summary>Gets whether the user is currently authenticated.</summary>
    public bool IsAuthenticated => _authState.IsAuthenticated;

    /// <summary>Gets the current user info.</summary>
    public UserInfo? CurrentUser => _authState.CurrentUser;

    /// <summary>Gets the current access token.</summary>
    public string? AccessToken => _authState.AccessToken;

    /// <summary>Event raised when authentication state changes.</summary>
    public event Action? OnAuthStateChanged
    {
        add => _authState.OnAuthStateChanged += value;
        remove => _authState.OnAuthStateChanged -= value;
    }

    /// <summary>
    /// Attempts to log in with the provided credentials.
    /// </summary>
    public async Task<(bool Success, string? Error)> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new LoginRequest { Email = email, Password = password };
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken);
                return (false, error?.Error ?? "Login failed");
            }

            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
            if (loginResponse is null)
            {
                return (false, "Invalid response from server");
            }

            _authState.SetAuthState(
                loginResponse.AccessToken,
                loginResponse.RefreshToken,
                loginResponse.ExpiresAt,
                loginResponse.User);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Attempts to refresh the access token.
    /// </summary>
    public async Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_authState.RefreshToken))
            return false;

        try
        {
            var request = new RefreshRequest { RefreshToken = _authState.RefreshToken };
            var response = await _httpClient.PostAsJsonAsync("api/auth/refresh", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Logout();
                return false;
            }

            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
            if (loginResponse is null)
            {
                Logout();
                return false;
            }

            _authState.SetAuthState(
                loginResponse.AccessToken,
                loginResponse.RefreshToken,
                loginResponse.ExpiresAt,
                loginResponse.User);
            return true;
        }
        catch
        {
            Logout();
            return false;
        }
    }

    /// <summary>
    /// Logs out the current user.
    /// </summary>
    public void Logout()
    {
        _authState.ClearAuthState();
    }

    private record ErrorResponse(string? Error);
}
