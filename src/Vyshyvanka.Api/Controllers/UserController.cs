using Vyshyvanka.Api.Authorization;
using Vyshyvanka.Api.Models;
using Vyshyvanka.Contracts;
using Vyshyvanka.Contracts.Auth;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Vyshyvanka.Api.Controllers;

/// <summary>
/// Admin endpoints for user management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = Policies.CanManageUsers)]
public class UserController(
    IUserRepository userRepository,
    IAuthService authService,
    AuthenticationSettings authSettings,
    ILogger<UserController> logger) : ControllerBase
{
    /// <summary>
    /// Lists all users with optional search filtering.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(UserListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserListResponse>> GetAll(
        [FromQuery] string? search = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);
        skip = Math.Max(0, skip);

        var allUsers = await userRepository.GetAllAsync(cancellationToken);
        var users = allUsers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            users = users.Where(u =>
                u.Email.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (u.DisplayName != null && u.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        var totalCount = users.Count();
        var page = users
            .OrderBy(u => u.Email)
            .Skip(skip)
            .Take(take)
            .Select(u => u.ToAdminResponse())
            .ToList();

        return Ok(new UserListResponse
        {
            Users = page,
            TotalCount = totalCount
        });
    }

    /// <summary>
    /// Gets a single user by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AdminUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound(new ApiError
            {
                Code = "USER_NOT_FOUND",
                Message = $"User with ID '{id}' was not found"
            });
        }

        return Ok(user.ToAdminResponse());
    }

    /// <summary>
    /// Creates a new user. Only available when using the BuiltIn authentication provider.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AdminUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminUserResponse>> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (authSettings.Provider is not AuthenticationProvider.BuiltIn)
        {
            return BadRequest(new ApiError
            {
                Code = "UNSUPPORTED",
                Message = $"Manual user creation is not available when using {authSettings.Provider} authentication. Users are provisioned automatically on login."
            });
        }

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
        {
            return BadRequest(new ApiError
            {
                Code = "INVALID_ROLE",
                Message = $"Invalid role '{request.Role}'. Valid values: Admin, Editor, Viewer."
            });
        }

        // Check if email already exists
        var existing = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
        {
            return BadRequest(new ApiError
            {
                Code = "EMAIL_EXISTS",
                Message = $"A user with email '{request.Email}' already exists"
            });
        }

        // Use the auth service to register (handles password hashing)
        var result = await authService.RegisterAsync(request.Email, request.Password, request.DisplayName, cancellationToken);
        if (!result.Success || result.User is null)
        {
            return BadRequest(new ApiError
            {
                Code = "USER_CREATION_FAILED",
                Message = result.ErrorMessage ?? "Failed to create user"
            });
        }

        // Update role and ensure active (admin-created users bypass approval)
        var user = result.User with
        {
            Role = role,
            IsActive = true
        };
        user = await userRepository.UpdateAsync(user, cancellationToken);

        logger.LogInformation("Admin created user {Email} with role {Role}", user.Email, user.Role);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user.ToAdminResponse());
    }

    /// <summary>
    /// Updates a user's profile (email and display name).
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AdminUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminUserResponse>> UpdateProfile(
        Guid id,
        [FromBody] UpdateUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound(new ApiError
            {
                Code = "USER_NOT_FOUND",
                Message = $"User with ID '{id}' was not found"
            });
        }

        // Check email uniqueness if changed
        if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (existing is not null)
            {
                return BadRequest(new ApiError
                {
                    Code = "EMAIL_EXISTS",
                    Message = $"A user with email '{request.Email}' already exists"
                });
            }
        }

        var updated = user with
        {
            Email = request.Email.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim()
        };
        updated = await userRepository.UpdateAsync(updated, cancellationToken);

        logger.LogInformation("Admin updated profile for user {UserId}: email={Email}, name={DisplayName}",
            id, updated.Email, updated.DisplayName);

        return Ok(updated.ToAdminResponse());
    }

    /// <summary>
    /// Updates a user's role.
    /// </summary>
    [HttpPut("{id:guid}/role")]
    [ProducesResponseType(typeof(AdminUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserResponse>> UpdateRole(
        Guid id,
        [FromBody] UpdateUserRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
        {
            return BadRequest(new ApiError
            {
                Code = "INVALID_ROLE",
                Message = $"Invalid role '{request.Role}'. Valid values: Admin, Editor, Viewer."
            });
        }

        var user = await userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound(new ApiError
            {
                Code = "USER_NOT_FOUND",
                Message = $"User with ID '{id}' was not found"
            });
        }

        var updated = user with { Role = role };
        updated = await userRepository.UpdateAsync(updated, cancellationToken);

        logger.LogInformation("Admin changed role for {Email} from {OldRole} to {NewRole}",
            user.Email, user.Role, role);

        return Ok(updated.ToAdminResponse());
    }

    /// <summary>
    /// Activates or deactivates a user.
    /// </summary>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(AdminUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserResponse>> UpdateStatus(
        Guid id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound(new ApiError
            {
                Code = "USER_NOT_FOUND",
                Message = $"User with ID '{id}' was not found"
            });
        }

        var updated = user with { IsActive = request.IsActive };
        updated = await userRepository.UpdateAsync(updated, cancellationToken);

        logger.LogInformation("Admin {Action} user {Email}",
            request.IsActive ? "activated" : "deactivated", user.Email);

        return Ok(updated.ToAdminResponse());
    }
}
