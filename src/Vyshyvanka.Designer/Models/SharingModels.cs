using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Designer.Models;

/// <summary>
/// Workflow permission response from the API.
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

/// <summary>
/// Request to share a workflow.
/// </summary>
public record ShareWorkflowRequest
{
    public PermissionTargetType TargetType { get; init; }
    public Guid TargetId { get; init; }
    public WorkflowPermissionLevel PermissionLevel { get; init; }
    public CredentialSharingPolicy CredentialPolicy { get; init; }
}
