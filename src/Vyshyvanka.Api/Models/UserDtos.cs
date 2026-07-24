using Vyshyvanka.Contracts.Auth;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Api.Models;

public static class UserMappings
{
    public static AdminUserResponse ToAdminResponse(this User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        Role = user.Role.ToString(),
        IsActive = user.IsActive,
        IsLockedOut = user.IsLockedOut,
        AuthenticationProvider = user.AuthenticationProvider.ToString(),
        CreatedAt = user.CreatedAt,
        LastLoginAt = user.LastLoginAt
    };
}
