using System.ComponentModel.DataAnnotations;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Api.Models;

/// <summary>
/// Request to share a workflow with a user or team.
/// </summary>
public record ShareWorkflowRequest
{
    /// <summary>Whether sharing with a user or a team.</summary>
    [Required]
    public PermissionTargetType TargetType { get; init; }

    /// <summary>The user or team ID to share with.</summary>
    [Required]
    public Guid TargetId { get; init; }

    /// <summary>Permission level to grant.</summary>
    [Required]
    public WorkflowPermissionLevel PermissionLevel { get; init; }

    /// <summary>
    /// How credentials are handled when the target executes the workflow.
    /// </summary>
    [Required]
    public CredentialSharingPolicy CredentialPolicy { get; init; }
}

/// <summary>
/// Workflow permission API response.
/// </summary>
public record WorkflowPermissionResponse
{
    public Guid Id { get; init; }
    public Guid WorkflowId { get; init; }
    public PermissionTargetType TargetType { get; init; }
    public Guid TargetId { get; init; }
    public string? TargetName { get; init; }
    public WorkflowPermissionLevel PermissionLevel { get; init; }
    public CredentialSharingPolicy CredentialPolicy { get; init; }
    public Guid GrantedBy { get; init; }
    public DateTime GrantedAt { get; init; }

    public static WorkflowPermissionResponse FromModel(WorkflowPermission permission, string? targetName = null) => new()
    {
        Id = permission.Id,
        WorkflowId = permission.WorkflowId,
        TargetType = permission.TargetType,
        TargetId = permission.TargetId,
        TargetName = targetName,
        PermissionLevel = permission.PermissionLevel,
        CredentialPolicy = permission.CredentialPolicy,
        GrantedBy = permission.GrantedBy,
        GrantedAt = permission.GrantedAt
    };
}
