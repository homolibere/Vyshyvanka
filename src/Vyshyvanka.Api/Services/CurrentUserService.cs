using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Api.Services;

/// <summary>
/// Extracts current user information from the HTTP context claims.
/// </summary>
internal sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public Guid? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            // Try JWT sub claim first, then fall back to NameIdentifier (used by API key auth)
            var userIdClaim = user.FindFirst(JwtRegisteredClaimNames.Sub)
                ?? user.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return null;
            }

            return userId;
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
}
