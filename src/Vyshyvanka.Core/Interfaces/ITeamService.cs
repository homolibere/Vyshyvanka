using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Core.Interfaces;

/// <summary>
/// Service for managing teams.
/// </summary>
public interface ITeamService
{
    /// <summary>Creates a new team owned by the specified user.</summary>
    Task<Team> CreateAsync(string name, string? description, Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>Gets a team by ID. Returns null if not found or user has no access.</summary>
    Task<Team?> GetByIdAsync(Guid teamId, Guid requestingUserId, CancellationToken cancellationToken = default);

    /// <summary>Gets all teams the user belongs to.</summary>
    Task<IReadOnlyList<Team>> GetUserTeamsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Updates team name/description. Only the owner can do this.</summary>
    Task<Team> UpdateAsync(Guid teamId, string name, string? description, Guid requestingUserId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a team. Only the owner can do this.</summary>
    Task<bool> DeleteAsync(Guid teamId, Guid requestingUserId, CancellationToken cancellationToken = default);

    /// <summary>Adds a user to a team. Only team owner can add members.</summary>
    Task AddMemberAsync(Guid teamId, Guid userId, TeamRole role, Guid requestingUserId, CancellationToken cancellationToken = default);

    /// <summary>Removes a user from a team. Owner can remove anyone; members can remove themselves.</summary>
    Task RemoveMemberAsync(Guid teamId, Guid userId, Guid requestingUserId, CancellationToken cancellationToken = default);
}
