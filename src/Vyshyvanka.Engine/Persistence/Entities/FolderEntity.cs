using System.ComponentModel.DataAnnotations;

namespace Vyshyvanka.Engine.Persistence.Entities;

/// <summary>
/// EF Core entity for folder persistence.
/// </summary>
public class FolderEntity
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(7)]
    public string? Color { get; set; }

    public Guid OwnerId { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation
    public UserEntity? Owner { get; set; }
    public ICollection<WorkflowEntity> Workflows { get; set; } = [];
}
