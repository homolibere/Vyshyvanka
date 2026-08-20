using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Contracts.Sharing;

/// <summary>
/// Request payload to grant a user or team access to a workflow.
/// </summary>
public record ShareWorkflowRequest
{
    /// <summary>Whether the grant targets an individual user or a team.</summary>
    public PermissionTargetType TargetType { get; init; }

    /// <summary>Identifier of the user or team receiving access, per <see cref="TargetType"/>.</summary>
    public Guid TargetId { get; init; }

    /// <summary>Level of access to grant. Levels are hierarchical (Edit implies Execute implies View).</summary>
    public WorkflowPermissionLevel PermissionLevel { get; init; }

    /// <summary>How credentials are resolved when the grantee executes the shared workflow.</summary>
    public CredentialSharingPolicy CredentialPolicy { get; init; }
}

/// <summary>
/// Represents an access grant on a workflow to a user or team.
/// </summary>
public record WorkflowPermissionResponse
{
    /// <summary>Unique identifier of the permission grant.</summary>
    public Guid Id { get; init; }

    /// <summary>Identifier of the workflow the grant applies to.</summary>
    public Guid WorkflowId { get; init; }

    /// <summary>Whether the grant targets an individual user or a team.</summary>
    public PermissionTargetType TargetType { get; init; }

    /// <summary>Identifier of the user or team the grant applies to, per <see cref="TargetType"/>.</summary>
    public Guid TargetId { get; init; }

    /// <summary>Display name of the target user or team, for convenience. <c>null</c> when unavailable.</summary>
    public string? TargetName { get; init; }

    /// <summary>Level of access granted. Levels are hierarchical (Edit implies Execute implies View).</summary>
    public WorkflowPermissionLevel PermissionLevel { get; init; }

    /// <summary>How credentials are resolved when the grantee executes the shared workflow.</summary>
    public CredentialSharingPolicy CredentialPolicy { get; init; }

    /// <summary>Identifier of the user who created the grant.</summary>
    public Guid GrantedBy { get; init; }

    /// <summary>UTC timestamp when the grant was created.</summary>
    public DateTime GrantedAt { get; init; }
}
