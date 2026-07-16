using System.ComponentModel.DataAnnotations;

namespace Vyshyvanka.Engine.Persistence.Entities;

/// <summary>
/// EF Core entity for team persistence.
/// </summary>
public class TeamEntity
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public Guid OwnerId { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation
    public UserEntity? Owner { get; set; }
    public ICollection<TeamMemberEntity> Members { get; set; } = [];
}
