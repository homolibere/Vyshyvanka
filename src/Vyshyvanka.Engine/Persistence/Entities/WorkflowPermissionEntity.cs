using System.ComponentModel.DataAnnotations;
using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Engine.Persistence.Entities;

/// <summary>
/// EF Core entity for workflow permission grants.
/// </summary>
public class WorkflowPermissionEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid WorkflowId { get; set; }

    public PermissionTargetType TargetType { get; set; }

    /// <summary>
    /// The target user or team ID depending on <see cref="TargetType"/>.
    /// </summary>
    public Guid TargetId { get; set; }

    public WorkflowPermissionLevel PermissionLevel { get; set; }

    public CredentialSharingPolicy CredentialPolicy { get; set; }

    public Guid GrantedBy { get; set; }

    public DateTime GrantedAt { get; set; }

    // Navigation
    public WorkflowEntity? Workflow { get; set; }
}
