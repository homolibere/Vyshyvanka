using System.ComponentModel.DataAnnotations;

namespace Vyshyvanka.Engine.Persistence.Entities;

/// <summary>
/// EF Core entity for workflow persistence.
/// </summary>
public class WorkflowEntity
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public int Version { get; set; }

    /// <summary>Activation state (Draft/Active/Paused). Only Active workflows respond to automatic triggers.</summary>
    public Vyshyvanka.Core.Enums.WorkflowStatus Status { get; set; }

    /// <summary>
    /// Scheduler cursor: the last scheduled fire time (UTC) the scheduler dispatched for this workflow.
    /// Null for workflows that have never been scheduled. Engine-internal; not surfaced over the API.
    /// </summary>
    public DateTime? LastScheduledFireAt { get; set; }

    /// <summary>JSON-serialized nodes.</summary>
    [Required]
    public string NodesJson { get; set; } = "[]";

    /// <summary>JSON-serialized connections.</summary>
    [Required]
    public string ConnectionsJson { get; set; } = "[]";

    /// <summary>JSON-serialized settings.</summary>
    public string? SettingsJson { get; set; }

    /// <summary>Comma-separated tags.</summary>
    public string? Tags { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid CreatedBy { get; set; }

    /// <summary>Optional folder for organization. Null means root.</summary>
    public Guid? FolderId { get; set; }

    // Navigation
    public FolderEntity? Folder { get; set; }
}
