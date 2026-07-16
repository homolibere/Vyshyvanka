using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Core.Interfaces;

/// <summary>
/// Repository for persisting and querying workflow permission grants.
/// </summary>
public interface IWorkflowPermissionRepository
{
    /// <summary>Creates a new permission grant.</summary>
    Task<WorkflowPermission> CreateAsync(WorkflowPermission permission, CancellationToken cancellationToken = default);

    /// <summary>Gets a permission by ID.</summary>
    Task<WorkflowPermission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets all permissions for a workflow.</summary>
    Task<IReadOnlyList<WorkflowPermission>> GetByWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default);

    /// <summary>Gets all permissions directly granted to a user (TargetType = User).</summary>
    Task<IReadOnlyList<WorkflowPermission>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Gets all permissions granted to any of the specified teams.</summary>
    Task<IReadOnlyList<WorkflowPermission>> GetByTeamsAsync(IEnumerable<Guid> teamIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the effective permission level a user has on a workflow,
    /// considering both direct user grants and team-based grants.
    /// Returns null if the user has no permission.
    /// </summary>
    Task<WorkflowPermission?> GetEffectivePermissionAsync(
        Guid workflowId,
        Guid userId,
        IEnumerable<Guid> userTeamIds,
        CancellationToken cancellationToken = default);

    /// <summary>Updates an existing permission (level or credential policy).</summary>
    Task<WorkflowPermission> UpdateAsync(WorkflowPermission permission, CancellationToken cancellationToken = default);

    /// <summary>Deletes a permission grant.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes all permissions for a workflow (used when deleting a workflow).</summary>
    Task DeleteByWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a specific grant already exists (prevents duplicates).
    /// </summary>
    Task<bool> ExistsAsync(
        Guid workflowId,
        PermissionTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken = default);
}
