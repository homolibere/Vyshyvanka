using System.Security.Claims;
using FlowForge.Api.Authorization;
using FlowForge.Api.Models;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.Api.Controllers;

/// <summary>
/// Credential management endpoints.
/// </summary>
[ApiController]
[Route("api/credentials")]
[Authorize(Policy = Policies.CanManageCredentials)]
public class CredentialController(
    ICredentialService credentialService,
    ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>List all credentials for the current user.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CredentialResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var credentials = await credentialService.ListAsync(userId.Value, cancellationToken);
        return Ok(credentials.Select(c => CredentialResponse.FromModel(c)));
    }

    /// <summary>Get a credential by ID (includes stored field keys for UI masking).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CredentialResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var credential = await credentialService.GetAsync(id, cancellationToken);
        if (credential is null || credential.OwnerId != userId)
        {
            return NotFound(new ApiError
            {
                Code = "CREDENTIAL_NOT_FOUND",
                Message = $"Credential with ID '{id}' was not found"
            });
        }

        // Decrypt to extract field keys only (values are discarded)
        IReadOnlyList<string>? storedFields = null;
        var decrypted = await credentialService.GetDecryptedAsync(id, cancellationToken);
        if (decrypted is not null)
        {
            storedFields = decrypted.Values.Keys
                .Where(k => !string.IsNullOrWhiteSpace(decrypted.Values[k]))
                .ToList();
        }

        return Ok(CredentialResponse.FromModel(credential, storedFields));
    }

    /// <summary>Create a new credential.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CredentialResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCredentialDto dto,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var validation = credentialService.ValidateCredentialData(dto.Type, dto.Data);
        if (!validation.IsValid)
        {
            return BadRequest(new ApiError
            {
                Code = "INVALID_CREDENTIAL_DATA",
                Message = string.Join("; ", validation.Errors.Select(e => e.Message))
            });
        }

        var request = new CreateCredentialRequest
        {
            Name = dto.Name,
            Type = dto.Type,
            Data = dto.Data,
            OwnerId = userId.Value
        };

        var credential = await credentialService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = credential.Id }, CredentialResponse.FromModel(credential));
    }

    /// <summary>Update an existing credential.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CredentialResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCredentialDto dto,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var existing = await credentialService.GetAsync(id, cancellationToken);
        if (existing is null || existing.OwnerId != userId)
        {
            return NotFound(new ApiError
            {
                Code = "CREDENTIAL_NOT_FOUND",
                Message = $"Credential with ID '{id}' was not found"
            });
        }

        if (dto.Data is not null)
        {
            var validation = credentialService.ValidateCredentialData(existing.Type, dto.Data);
            if (!validation.IsValid)
            {
                return BadRequest(new ApiError
                {
                    Code = "INVALID_CREDENTIAL_DATA",
                    Message = string.Join("; ", validation.Errors.Select(e => e.Message))
                });
            }
        }

        var request = new UpdateCredentialRequest { Name = dto.Name, Data = dto.Data };
        var updated = await credentialService.UpdateAsync(id, request, cancellationToken);
        return Ok(CredentialResponse.FromModel(updated));
    }

    /// <summary>Delete a credential.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var existing = await credentialService.GetAsync(id, cancellationToken);
        if (existing is null || existing.OwnerId != userId)
        {
            return NotFound(new ApiError
            {
                Code = "CREDENTIAL_NOT_FOUND",
                Message = $"Credential with ID '{id}' was not found"
            });
        }

        await credentialService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null && Guid.TryParse(claim.Value, out var id) ? id : currentUserService.UserId;
    }
}
