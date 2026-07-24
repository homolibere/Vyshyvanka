using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Contracts.Sharing;

/// <summary>
/// Request to share a workflow with a user or team.
/// </summary>
public record ShareWorkflowRequest
{
    public PermissionTargetType TargetType { get; init; }
    public Guid TargetId { get; init; }
    public WorkflowPermissionLevel PermissionLevel { get; init; }
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
}
