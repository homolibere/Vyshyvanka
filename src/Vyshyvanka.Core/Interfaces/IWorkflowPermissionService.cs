using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Core.Interfaces;

/// <summary>
/// Service for checking and managing workflow permissions.
/// </summary>
public interface IWorkflowPermissionService
{
    /// <summary>
    /// Checks whether a user has the required permission level on a workflow.
    /// Considers ownership, admin role, and shared grants (user + team).
    /// </summary>
    Task<bool> HasPermissionAsync(
        Guid workflowId,
        Guid workflowOwnerId,
        Guid userId,
        WorkflowPermissionLevel requiredLevel,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the credential sharing policy for a specific user on a workflow.
    /// Returns null if the user has no shared permission (owner/admin don't need this).
    /// </summary>
    Task<CredentialSharingPolicy?> GetCredentialPolicyAsync(
        Guid workflowId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Grants a permission to a user or team on a workflow.
    /// If a permission already exists for the same target, it is updated.
    /// </summary>
    Task<WorkflowPermission> GrantPermissionAsync(
        Guid workflowId,
        PermissionTargetType targetType,
        Guid targetId,
        WorkflowPermissionLevel level,
        CredentialSharingPolicy credentialPolicy,
        Guid grantedBy,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes a permission grant.</summary>
    Task<bool> RevokePermissionAsync(Guid permissionId, CancellationToken cancellationToken = default);

    /// <summary>Gets all permission grants for a workflow.</summary>
    Task<IReadOnlyList<WorkflowPermission>> GetWorkflowPermissionsAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all workflow IDs shared with a user (directly or via teams).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetSharedWorkflowIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
