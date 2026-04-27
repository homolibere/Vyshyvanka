using System.ComponentModel.DataAnnotations;

namespace FlowForge.Engine.Persistence.Entities;

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
    
    public bool IsActive { get; set; }
    
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
}
