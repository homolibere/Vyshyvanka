using Vyshyvanka.Contracts.Teams;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Api.Models;

public static class TeamMappings
{
    public static TeamResponse ToResponse(this Team team) => new()
    {
        Id = team.Id,
        Name = team.Name,
        Description = team.Description,
        OwnerId = team.OwnerId,
        Members = team.Members.Select(m => m.ToResponse()).ToList(),
        CreatedAt = team.CreatedAt
    };

    public static TeamMemberResponse ToResponse(this TeamMember member) => new()
    {
        UserId = member.UserId,
        DisplayName = member.DisplayName,
        Email = member.Email,
        Role = member.Role,
        JoinedAt = member.JoinedAt
    };
}
