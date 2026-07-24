using Vyshyvanka.Api.Authorization;
using Vyshyvanka.Api.Models;
using Vyshyvanka.Contracts;
using Vyshyvanka.Contracts.Folders;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Vyshyvanka.Api.Controllers;

/// <summary>
/// API controller for workflow folder management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class FolderController(
    IFolderRepository folderRepository,
    ICurrentUserService currentUserService,
    ILogger<FolderController> logger) : ControllerBase
{
    /// <summary>
    /// Gets all folders for the current user.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.CanViewWorkflows)]
    [ProducesResponseType(typeof(List<FolderResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<FolderResponse>>> GetAll(CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return Unauthorized();

        var folders = await folderRepository.GetByOwnerAsync(userId.Value, cancellationToken);
        return Ok(folders.Select(f => f.ToResponse()).ToList());
    }

    /// <summary>
    /// Gets a folder by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.CanViewWorkflows)]
    [ProducesResponseType(typeof(FolderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FolderResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var folder = await folderRepository.GetByIdAsync(id, cancellationToken);
        if (folder is null || !IsOwnerOrAdmin(folder))
        {
            return NotFound(new ApiError
            {
                Code = "FOLDER_NOT_FOUND",
                Message = $"Folder with ID '{id}' was not found"
            });
        }

        return Ok(folder.ToResponse());
    }

    /// <summary>
    /// Creates a new folder.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.CanManageWorkflows)]
    [ProducesResponseType(typeof(FolderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FolderResponse>> Create(
        [FromBody] CreateFolderRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;

        if (await folderRepository.ExistsByNameAsync(userId, request.Name, cancellationToken))
        {
            return BadRequest(new ApiError
            {
                Code = "FOLDER_NAME_EXISTS",
                Message = $"A folder named '{request.Name}' already exists"
            });
        }

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Color = request.Color,
            OwnerId = userId,
            CreatedAt = DateTime.UtcNow
        };

        var created = await folderRepository.CreateAsync(folder, cancellationToken);
        logger.LogInformation("Created folder {FolderId}: {FolderName}", created.Id, created.Name);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.ToResponse());
    }

    /// <summary>
    /// Updates a folder.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanManageWorkflows)]
    [ProducesResponseType(typeof(FolderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FolderResponse>> Update(
        Guid id,
        [FromBody] UpdateFolderRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await folderRepository.GetByIdAsync(id, cancellationToken);
        if (existing is null || !IsOwnerOrAdmin(existing))
        {
            return NotFound(new ApiError
            {
                Code = "FOLDER_NOT_FOUND",
                Message = $"Folder with ID '{id}' was not found"
            });
        }

        var updated = existing with
        {
            Name = request.Name,
            Color = request.Color
        };

        var result = await folderRepository.UpdateAsync(updated, cancellationToken);
        logger.LogInformation("Updated folder {FolderId}: {FolderName}", result.Id, result.Name);

        return Ok(result.ToResponse());
    }

    /// <summary>
    /// Deletes a folder. Workflows in this folder are moved to root.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.CanManageWorkflows)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await folderRepository.GetByIdAsync(id, cancellationToken);
        if (existing is null || !IsOwnerOrAdmin(existing))
        {
            return NotFound(new ApiError
            {
                Code = "FOLDER_NOT_FOUND",
                Message = $"Folder with ID '{id}' was not found"
            });
        }

        await folderRepository.DeleteAsync(id, cancellationToken);
        logger.LogInformation("Deleted folder {FolderId}", id);

        return NoContent();
    }

    private bool IsOwnerOrAdmin(Folder folder)
    {
        if (User.IsInRole(Roles.Admin))
            return true;

        var userId = currentUserService.UserId;
        return userId is not null && folder.OwnerId == userId;
    }
}
