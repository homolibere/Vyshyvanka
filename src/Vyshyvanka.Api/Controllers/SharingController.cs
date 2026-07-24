using Vyshyvanka.Api.Authorization;
using Vyshyvanka.Api.Models;
using Vyshyvanka.Contracts;
using Vyshyvanka.Contracts.Sharing;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Vyshyvanka.Api.Controllers;

/// <summary>
/// API controller for workflow sharing and permissions.
/// </summary>
[ApiController]
[Route("api/workflow/{workflowId:guid}/sharing")]
[Produces("application/json")]
public class SharingController(
    IWorkflowPermissionService permissionService,
    IWorkflowRepository workflowRepository,
    IUserRepository userRepository,
    ITeamRepository teamRepository,
    ICurrentUserService currentUserService,
    ILogger<SharingController> logger) : ControllerBase
{
    /// <summary>
    /// Gets all permission grants for a workflow.
    /// Only the owner or admin can view sharing settings.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.CanViewWorkflows)]
    [ProducesResponseType(typeof(List<WorkflowPermissionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<WorkflowPermissionResponse>>> GetPermissions(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        var workflow = await workflowRepository.GetByIdAsync(workflowId, cancellationToken);
        if (workflow is null || !IsOwnerOrAdmin(workflow.CreatedBy))
        {
            return NotFound(new ApiError
            {
                Code = "WORKFLOW_NOT_FOUND",
                Message = $"Workflow with ID '{workflowId}' was not found"
            });
        }

        var permissions = await permissionService.GetWorkflowPermissionsAsync(workflowId, cancellationToken);

        var responses = new List<WorkflowPermissionResponse>(permissions.Count);
        foreach (var perm in permissions)
        {
            var targetName = await ResolveTargetNameAsync(perm.TargetType, perm.TargetId, cancellationToken);
            responses.Add(perm.ToResponse(targetName));
        }

        return Ok(responses);
    }

    /// <summary>
    /// Shares a workflow with a user or team.
    /// Only the owner or admin can share a workflow.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.CanManageWorkflows)]
    [ProducesResponseType(typeof(WorkflowPermissionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<WorkflowPermissionResponse>> Share(
        Guid workflowId,
        [FromBody] ShareWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        var workflow = await workflowRepository.GetByIdAsync(workflowId, cancellationToken);
        if (workflow is null)
        {
            return NotFound(new ApiError
            {
                Code = "WORKFLOW_NOT_FOUND",
                Message = $"Workflow with ID '{workflowId}' was not found"
            });
        }

        if (!IsOwnerOrAdmin(workflow.CreatedBy))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError
            {
                Code = "FORBIDDEN",
                Message = "Only the workflow owner can manage sharing"
            });
        }

        // Prevent sharing with yourself
        if (request.TargetType == PermissionTargetType.User && request.TargetId == workflow.CreatedBy)
        {
            return BadRequest(new ApiError
            {
                Code = "CANNOT_SHARE_WITH_SELF",
                Message = "Cannot share a workflow with yourself — you already have full access as the owner"
            });
        }

        var userId = currentUserService.UserId ?? Guid.Empty;

        var permission = await permissionService.GrantPermissionAsync(
            workflowId,
            request.TargetType,
            request.TargetId,
            request.PermissionLevel,
            request.CredentialPolicy,
            userId,
            cancellationToken);

        logger.LogInformation(
            "Shared workflow {WorkflowId} with {TargetType} {TargetId} at level {Level}",
            workflowId, request.TargetType, request.TargetId, request.PermissionLevel);

        return CreatedAtAction(
            nameof(GetPermissions),
            new { workflowId },
            permission.ToResponse());
    }

    /// <summary>
    /// Revokes a permission grant.
    /// Only the owner or admin can revoke sharing.
    /// </summary>
    [HttpDelete("{permissionId:guid}")]
    [Authorize(Policy = Policies.CanManageWorkflows)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Revoke(
        Guid workflowId,
        Guid permissionId,
        CancellationToken cancellationToken = default)
    {
        var workflow = await workflowRepository.GetByIdAsync(workflowId, cancellationToken);
        if (workflow is null)
        {
            return NotFound(new ApiError
            {
                Code = "WORKFLOW_NOT_FOUND",
                Message = $"Workflow with ID '{workflowId}' was not found"
            });
        }

        if (!IsOwnerOrAdmin(workflow.CreatedBy))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError
            {
                Code = "FORBIDDEN",
                Message = "Only the workflow owner can manage sharing"
            });
        }

        var deleted = await permissionService.RevokePermissionAsync(permissionId, cancellationToken);
        if (!deleted)
        {
            return NotFound(new ApiError
            {
                Code = "PERMISSION_NOT_FOUND",
                Message = $"Permission with ID '{permissionId}' was not found"
            });
        }

        logger.LogInformation("Revoked permission {PermissionId} on workflow {WorkflowId}", permissionId, workflowId);
        return NoContent();
    }

    private bool IsOwnerOrAdmin(Guid ownerId)
    {
        if (User.IsInRole(Roles.Admin))
            return true;

        var userId = currentUserService.UserId;
        return userId is not null && ownerId == userId;
    }

    private async Task<string?> ResolveTargetNameAsync(
        PermissionTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        if (targetType == PermissionTargetType.User)
        {
            var user = await userRepository.GetByIdAsync(targetId, cancellationToken);
            return user?.DisplayName ?? user?.Email;
        }

        if (targetType == PermissionTargetType.Team)
        {
            var team = await teamRepository.GetByIdAsync(targetId, cancellationToken);
            return team?.Name;
        }

        return null;
    }
}
