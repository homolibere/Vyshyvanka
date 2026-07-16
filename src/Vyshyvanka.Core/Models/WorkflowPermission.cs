using System.Text.Json.Serialization;
using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Core.Models;

/// <summary>
/// Represents a permission grant on a workflow — either to a specific user or a team.
/// </summary>
public record WorkflowPermission
{
    /// <summary>Unique identifier for this permission grant.</summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    /// <summary>The workflow this permission applies to.</summary>
    [JsonPropertyName("workflowId")]
    public Guid WorkflowId { get; init; }

    /// <summary>Whether this permission targets a user or a team.</summary>
    [JsonPropertyName("targetType")]
    public PermissionTargetType TargetType { get; init; }

    /// <summary>
    /// The ID of the target user or team.
    /// Interpretation depends on <see cref="TargetType"/>.
    /// </summary>
    [JsonPropertyName("targetId")]
    public Guid TargetId { get; init; }

    /// <summary>The level of access granted.</summary>
    [JsonPropertyName("permissionLevel")]
    public WorkflowPermissionLevel PermissionLevel { get; init; }

    /// <summary>
    /// How credentials are resolved when the target executes this workflow.
    /// </summary>
    [JsonPropertyName("credentialPolicy")]
    public CredentialSharingPolicy CredentialPolicy { get; init; }

    /// <summary>User who granted this permission (the workflow owner).</summary>
    [JsonPropertyName("grantedBy")]
    public Guid GrantedBy { get; init; }

    /// <summary>When this permission was granted.</summary>
    [JsonPropertyName("grantedAt")]
    public DateTime GrantedAt { get; init; }
}
