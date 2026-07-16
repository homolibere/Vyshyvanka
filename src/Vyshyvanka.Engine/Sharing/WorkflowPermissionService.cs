using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Engine.Sharing;

/// <summary>
/// Service for checking and managing workflow permissions.
/// Centralizes permission logic that was previously handled by IsOwnerOrAdmin checks.
/// </summary>
public class WorkflowPermissionService(
    IWorkflowPermissionRepository permissionRepository,
    ITeamRepository teamRepository) : IWorkflowPermissionService
{
    /// <inheritdoc />
    public async Task<bool> HasPermissionAsync(
        Guid workflowId,
        Guid workflowOwnerId,
        Guid userId,
        WorkflowPermissionLevel requiredLevel,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        // Admin always has access
        if (isAdmin)
            return true;

        // Owner always has full access
        if (workflowOwnerId == userId)
            return true;

        // Check shared permissions
        var teamIds = await GetUserTeamIdsAsync(userId, cancellationToken);
        var effectivePermission = await permissionRepository.GetEffectivePermissionAsync(
            workflowId, userId, teamIds, cancellationToken);

        if (effectivePermission is null)
            return false;

        // Permission levels are hierarchical: Edit > Execute > View
        return effectivePermission.PermissionLevel >= requiredLevel;
    }

    /// <inheritdoc />
    public async Task<CredentialSharingPolicy?> GetCredentialPolicyAsync(
        Guid workflowId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var teamIds = await GetUserTeamIdsAsync(userId, cancellationToken);
        var effectivePermission = await permissionRepository.GetEffectivePermissionAsync(
            workflowId, userId, teamIds, cancellationToken);

        return effectivePermission?.CredentialPolicy;
    }

    /// <inheritdoc />
    public async Task<WorkflowPermission> GrantPermissionAsync(
        Guid workflowId,
        PermissionTargetType targetType,
        Guid targetId,
        WorkflowPermissionLevel level,
        CredentialSharingPolicy credentialPolicy,
        Guid grantedBy,
        CancellationToken cancellationToken = default)
    {
        // Check if permission already exists
        var exists = await permissionRepository.ExistsAsync(workflowId, targetType, targetId, cancellationToken);
        if (exists)
        {
            // Update existing permission
            var existing = (await permissionRepository.GetByWorkflowAsync(workflowId, cancellationToken))
                .First(p => p.TargetType == targetType && p.TargetId == targetId);

            var updated = existing with
            {
                PermissionLevel = level,
                CredentialPolicy = credentialPolicy
            };

            return await permissionRepository.UpdateAsync(updated, cancellationToken);
        }

        var permission = new WorkflowPermission
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            TargetType = targetType,
            TargetId = targetId,
            PermissionLevel = level,
            CredentialPolicy = credentialPolicy,
            GrantedBy = grantedBy,
            GrantedAt = DateTime.UtcNow
        };

        return await permissionRepository.CreateAsync(permission, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RevokePermissionAsync(Guid permissionId, CancellationToken cancellationToken = default)
    {
        return await permissionRepository.DeleteAsync(permissionId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkflowPermission>> GetWorkflowPermissionsAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        return await permissionRepository.GetByWorkflowAsync(workflowId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetSharedWorkflowIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var teamIds = await GetUserTeamIdsAsync(userId, cancellationToken);

        var directPermissions = await permissionRepository.GetByUserAsync(userId, cancellationToken);
        var teamPermissions = await permissionRepository.GetByTeamsAsync(teamIds, cancellationToken);

        return directPermissions
            .Concat(teamPermissions)
            .Select(p => p.WorkflowId)
            .Distinct()
            .ToList();
    }

    private async Task<List<Guid>> GetUserTeamIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var teams = await teamRepository.GetByUserAsync(userId, cancellationToken);
        return teams.Select(t => t.Id).ToList();
    }
}
