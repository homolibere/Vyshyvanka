using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Engine.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Vyshyvanka.Engine.Persistence;

/// <summary>
/// EF Core implementation of the team repository.
/// </summary>
public class TeamRepository(VyshyvankaDbContext context) : ITeamRepository
{
    /// <inheritdoc />
    public async Task<Team> CreateAsync(Team team, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(team);

        var entity = new TeamEntity
        {
            Id = team.Id,
            Name = team.Name,
            Description = team.Description,
            OwnerId = team.OwnerId,
            CreatedAt = team.CreatedAt
        };

        // Add the owner as the first member with Owner role
        entity.Members.Add(new TeamMemberEntity
        {
            TeamId = team.Id,
            UserId = team.OwnerId,
            Role = TeamRole.Owner,
            JoinedAt = team.CreatedAt
        });

        context.Teams.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(team.Id, cancellationToken) ?? throw new InvalidOperationException("Failed to read back created team");
    }

    /// <inheritdoc />
    public async Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Teams
            .AsNoTracking()
            .Include(t => t.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return entity is null ? null : ToModel(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Team>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await context.Teams
            .AsNoTracking()
            .Include(t => t.Members)
                .ThenInclude(m => m.User)
            .Where(t => t.Members.Any(m => m.UserId == userId))
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(ToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Team>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var entities = await context.Teams
            .AsNoTracking()
            .Include(t => t.Members)
                .ThenInclude(m => m.User)
            .Where(t => t.OwnerId == ownerId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(ToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<Team> UpdateAsync(Team team, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(team);

        var entity = await context.Teams
            .FirstOrDefaultAsync(t => t.Id == team.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Team {team.Id} not found");

        entity.Name = team.Name;
        entity.Description = team.Description;

        await context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(team.Id, cancellationToken) ?? throw new InvalidOperationException("Failed to read back updated team");
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Teams.FindAsync([id], cancellationToken);
        if (entity is null)
            return false;

        context.Teams.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task AddMemberAsync(Guid teamId, TeamMember member, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(member);

        var entity = new TeamMemberEntity
        {
            TeamId = teamId,
            UserId = member.UserId,
            Role = member.Role,
            JoinedAt = member.JoinedAt
        };

        context.TeamMembers.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await context.TeamMembers
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId, cancellationToken);

        if (entity is not null)
        {
            context.TeamMembers.Remove(entity);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.TeamMembers
            .AnyAsync(m => m.TeamId == teamId && m.UserId == userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByNameAsync(Guid ownerId, string name, CancellationToken cancellationToken = default)
    {
        return await context.Teams
            .AnyAsync(t => t.OwnerId == ownerId && t.Name == name, cancellationToken);
    }

    private static Team ToModel(TeamEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        OwnerId = entity.OwnerId,
        Members = entity.Members.Select(m => new TeamMember
        {
            UserId = m.UserId,
            DisplayName = m.User?.DisplayName,
            Email = m.User?.Email,
            Role = m.Role,
            JoinedAt = m.JoinedAt
        }).ToList(),
        CreatedAt = entity.CreatedAt
    };
}
