using System.Net.Http.Headers;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// HTTP message handler that adds the Authorization header with the Bearer token.
/// Depends on AuthStateService (singleton) to avoid circular dependency with HttpClient.
/// </summary>
public class AuthorizationMessageHandler : DelegatingHandler
{
    private readonly AuthStateService _authState;

    public AuthorizationMessageHandler(AuthStateService authState)
    {
        _authState = authState;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_authState.IsAuthenticated && !string.IsNullOrEmpty(_authState.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authState.AccessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
