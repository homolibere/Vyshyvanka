using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Engine.Persistence.Entities;

/// <summary>
/// EF Core entity for team membership (junction table).
/// </summary>
public class TeamMemberEntity
{
    public Guid TeamId { get; set; }

    public Guid UserId { get; set; }

    public TeamRole Role { get; set; }

    public DateTime JoinedAt { get; set; }

    // Navigation
    public TeamEntity? Team { get; set; }
    public UserEntity? User { get; set; }
}
