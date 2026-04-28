using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Api.Extensions;

/// <summary>
/// Seeds development users on application startup.
/// </summary>
public static class DevelopmentUserSeeder
{
    /// <summary>
    /// Seeds default development users if they don't exist.
    /// Only runs when the built-in authentication provider is active.
    /// </summary>
    public static async Task SeedDevelopmentUsersAsync(IServiceProvider services)
    {
        var logger = services.GetService<ILogger<Program>>();

        var authSettings = services.GetRequiredService<AuthenticationSettings>();
        if (authSettings.Provider is not AuthenticationProvider.BuiltIn)
        {
            logger?.LogInformation(
                "Skipping development user seeding — authentication provider is {Provider}",
                authSettings.Provider);
            return;
        }

        using var scope = services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var devUsers = new[]
        {
            ("admin@vyshyvanka.local", "Admin123!", "Admin User", UserRole.Admin),
            ("editor@vyshyvanka.local", "Editor123!", "Editor User", UserRole.Editor),
            ("viewer@vyshyvanka.local", "Viewer123!", "Viewer User", UserRole.Viewer)
        };

        foreach (var (email, password, displayName, role) in devUsers)
        {
            var existingUser = await userRepository.GetByEmailAsync(email);
            if (existingUser is not null)
            {
                logger?.LogDebug("Development user {Email} already exists", email);
                continue;
            }

            var result = await authService.RegisterAsync(email, password, displayName);
            if (result.Success && result.User is not null)
            {
                // Update role if not Admin (RegisterAsync creates Editor by default)
                if (role != UserRole.Editor)
                {
                    var user = result.User with { Role = role };
                    await userRepository.UpdateAsync(user);
                }

                logger?.LogInformation("Created development user: {Email} ({Role})", email, role);
            }
            else
            {
                logger?.LogWarning("Failed to create development user {Email}: {Error}", email, result.ErrorMessage);
            }
        }
    }
}
