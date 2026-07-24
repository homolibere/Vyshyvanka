using System.Security.Claims;
using Vyshyvanka.Contracts.Auth;
using Vyshyvanka.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Vyshyvanka.Api.Controllers;

/// <summary>
/// API key management endpoints.
/// </summary>
[ApiController]
[Route("api/apikeys")]
[Authorize]
public class ApiKeyController(IApiKeyService apiKeyService) : ControllerBase
{

    /// <summary>
    /// Create a new API key.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApiKeyRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Name is required" });
        }

        var result = await apiKeyService.CreateAsync(
            userId.Value,
            request.Name,
            request.Scopes,
            request.ExpiresAt,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new CreateApiKeyResponse
        {
            Id = result.ApiKey!.Id,
            Name = result.ApiKey.Name,
            Key = result.PlainTextKey!,
            Scopes = result.ApiKey.Scopes,
            CreatedAt = result.ApiKey.CreatedAt,
            ExpiresAt = result.ApiKey.ExpiresAt
        });
    }

    /// <summary>
    /// List all API keys for the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var keys = await apiKeyService.GetByUserIdAsync(userId.Value, cancellationToken);

        return Ok(keys.Select(k => new ApiKeyResponse
        {
            Id = k.Id,
            Name = k.Name,
            Scopes = k.Scopes,
            CreatedAt = k.CreatedAt,
            ExpiresAt = k.ExpiresAt,
            LastUsedAt = k.LastUsedAt,
            IsActive = k.IsActive
        }));
    }

    /// <summary>
    /// Get an API key by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var key = await apiKeyService.GetByIdAsync(id, cancellationToken);
        if (key is null || key.UserId != userId)
        {
            return NotFound();
        }

        return Ok(new ApiKeyResponse
        {
            Id = key.Id,
            Name = key.Name,
            Scopes = key.Scopes,
            CreatedAt = key.CreatedAt,
            ExpiresAt = key.ExpiresAt,
            LastUsedAt = key.LastUsedAt,
            IsActive = key.IsActive
        });
    }

    /// <summary>
    /// Revoke an API key.
    /// </summary>
    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var key = await apiKeyService.GetByIdAsync(id, cancellationToken);
        if (key is null || key.UserId != userId)
        {
            return NotFound();
        }

        await apiKeyService.RevokeAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Delete an API key.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var key = await apiKeyService.GetByIdAsync(id, cancellationToken);
        if (key is null || key.UserId != userId)
        {
            return NotFound();
        }

        await apiKeyService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return null;
        }
        return userId;
    }
}
