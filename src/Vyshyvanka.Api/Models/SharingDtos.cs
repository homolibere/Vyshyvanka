using Vyshyvanka.Contracts.Sharing;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Api.Models;

public static class SharingMappings
{
    public static WorkflowPermissionResponse ToResponse(this WorkflowPermission permission, string? targetName = null) => new()
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
