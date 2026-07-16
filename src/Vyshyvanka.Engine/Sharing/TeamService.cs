using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Engine.Sharing;

/// <summary>
/// Service for managing teams and their members.
/// </summary>
public class TeamService(ITeamRepository teamRepository) : ITeamService
{
    /// <inheritdoc />
    public async Task<Team> CreateAsync(string name, string? description, Guid ownerId, CancellationToken cancellationToken = default)
    {
        if (await teamRepository.ExistsByNameAsync(ownerId, name, cancellationToken))
            throw new InvalidOperationException($"A team named '{name}' already exists");

        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow
        };

        return await teamRepository.CreateAsync(team, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Team?> GetByIdAsync(Guid teamId, Guid requestingUserId, CancellationToken cancellationToken = default)
    {
        var team = await teamRepository.GetByIdAsync(teamId, cancellationToken);
        if (team is null)
            return null;

        // Only members can see a team
        if (!team.Members.Any(m => m.UserId == requestingUserId))
            return null;

        return team;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Team>> GetUserTeamsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await teamRepository.GetByUserAsync(userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Team> UpdateAsync(Guid teamId, string name, string? description, Guid requestingUserId, CancellationToken cancellationToken = default)
    {
        var team = await teamRepository.GetByIdAsync(teamId, cancellationToken)
            ?? throw new InvalidOperationException($"Team {teamId} not found");

        if (team.OwnerId != requestingUserId)
            throw new UnauthorizedAccessException("Only the team owner can update team settings");

        var updated = team with
        {
            Name = name,
            Description = description
        };

        return await teamRepository.UpdateAsync(updated, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid teamId, Guid requestingUserId, CancellationToken cancellationToken = default)
    {
        var team = await teamRepository.GetByIdAsync(teamId, cancellationToken);
        if (team is null)
            return false;

        if (team.OwnerId != requestingUserId)
            throw new UnauthorizedAccessException("Only the team owner can delete the team");

        return await teamRepository.DeleteAsync(teamId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddMemberAsync(Guid teamId, Guid userId, TeamRole role, Guid requestingUserId, CancellationToken cancellationToken = default)
    {
        var team = await teamRepository.GetByIdAsync(teamId, cancellationToken)
            ?? throw new InvalidOperationException($"Team {teamId} not found");

        if (team.OwnerId != requestingUserId)
            throw new UnauthorizedAccessException("Only the team owner can add members");

        if (await teamRepository.IsMemberAsync(teamId, userId, cancellationToken))
            throw new InvalidOperationException("User is already a member of this team");

        var member = new TeamMember
        {
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };

        await teamRepository.AddMemberAsync(teamId, member, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveMemberAsync(Guid teamId, Guid userId, Guid requestingUserId, CancellationToken cancellationToken = default)
    {
        var team = await teamRepository.GetByIdAsync(teamId, cancellationToken)
            ?? throw new InvalidOperationException($"Team {teamId} not found");

        // Owner can remove anyone; members can remove themselves
        if (team.OwnerId != requestingUserId && userId != requestingUserId)
            throw new UnauthorizedAccessException("Only the team owner can remove other members");

        // Owner cannot be removed
        if (userId == team.OwnerId)
            throw new InvalidOperationException("Cannot remove the team owner. Transfer ownership or delete the team instead.");

        await teamRepository.RemoveMemberAsync(teamId, userId, cancellationToken);
    }
}
