using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Engine.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Vyshyvanka.Engine.Persistence;

/// <summary>
/// EF Core implementation of the workflow permission repository.
/// </summary>
public class WorkflowPermissionRepository(VyshyvankaDbContext context) : IWorkflowPermissionRepository
{
    /// <inheritdoc />
    public async Task<WorkflowPermission> CreateAsync(WorkflowPermission permission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permission);

        var entity = ToEntity(permission);
        context.WorkflowPermissions.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return ToModel(entity);
    }

    /// <inheritdoc />
    public async Task<WorkflowPermission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.WorkflowPermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return entity is null ? null : ToModel(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkflowPermission>> GetByWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        var entities = await context.WorkflowPermissions
            .AsNoTracking()
            .Where(p => p.WorkflowId == workflowId)
            .OrderBy(p => p.GrantedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(ToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkflowPermission>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await context.WorkflowPermissions
            .AsNoTracking()
            .Where(p => p.TargetType == PermissionTargetType.User && p.TargetId == userId)
            .ToListAsync(cancellationToken);

        return entities.Select(ToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkflowPermission>> GetByTeamsAsync(IEnumerable<Guid> teamIds, CancellationToken cancellationToken = default)
    {
        var teamIdList = teamIds.ToList();
        if (teamIdList.Count == 0)
            return [];

        var entities = await context.WorkflowPermissions
            .AsNoTracking()
            .Where(p => p.TargetType == PermissionTargetType.Team && teamIdList.Contains(p.TargetId))
            .ToListAsync(cancellationToken);

        return entities.Select(ToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<WorkflowPermission?> GetEffectivePermissionAsync(
        Guid workflowId,
        Guid userId,
        IEnumerable<Guid> userTeamIds,
        CancellationToken cancellationToken = default)
    {
        var teamIdList = userTeamIds.ToList();

        // Find all grants that apply to this user for this workflow
        var grants = await context.WorkflowPermissions
            .AsNoTracking()
            .Where(p => p.WorkflowId == workflowId &&
                ((p.TargetType == PermissionTargetType.User && p.TargetId == userId) ||
                 (p.TargetType == PermissionTargetType.Team && teamIdList.Contains(p.TargetId))))
            .ToListAsync(cancellationToken);

        if (grants.Count == 0)
            return null;

        // Return the grant with the highest permission level
        var best = grants.OrderByDescending(g => g.PermissionLevel).First();
        return ToModel(best);
    }

    /// <inheritdoc />
    public async Task<WorkflowPermission> UpdateAsync(WorkflowPermission permission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permission);

        var entity = await context.WorkflowPermissions
            .FirstOrDefaultAsync(p => p.Id == permission.Id, cancellationToken)
            ?? throw new InvalidOperationException($"WorkflowPermission {permission.Id} not found");

        entity.PermissionLevel = permission.PermissionLevel;
        entity.CredentialPolicy = permission.CredentialPolicy;

        await context.SaveChangesAsync(cancellationToken);

        return ToModel(entity);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.WorkflowPermissions.FindAsync([id], cancellationToken);
        if (entity is null)
            return false;

        context.WorkflowPermissions.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task DeleteByWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        await context.WorkflowPermissions
            .Where(p => p.WorkflowId == workflowId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(
        Guid workflowId,
        PermissionTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        return await context.WorkflowPermissions
            .AnyAsync(p => p.WorkflowId == workflowId &&
                           p.TargetType == targetType &&
                           p.TargetId == targetId, cancellationToken);
    }

    private static WorkflowPermissionEntity ToEntity(WorkflowPermission permission) => new()
    {
        Id = permission.Id,
        WorkflowId = permission.WorkflowId,
        TargetType = permission.TargetType,
        TargetId = permission.TargetId,
        PermissionLevel = permission.PermissionLevel,
        CredentialPolicy = permission.CredentialPolicy,
        GrantedBy = permission.GrantedBy,
        GrantedAt = permission.GrantedAt
    };

    private static WorkflowPermission ToModel(WorkflowPermissionEntity entity) => new()
    {
        Id = entity.Id,
        WorkflowId = entity.WorkflowId,
        TargetType = entity.TargetType,
        TargetId = entity.TargetId,
        PermissionLevel = entity.PermissionLevel,
        CredentialPolicy = entity.CredentialPolicy,
        GrantedBy = entity.GrantedBy,
        GrantedAt = entity.GrantedAt
    };
}
