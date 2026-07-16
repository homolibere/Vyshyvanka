using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Core.Interfaces;

/// <summary>
/// Repository for persisting and querying teams and their members.
/// </summary>
public interface ITeamRepository
{
    /// <summary>Creates a new team.</summary>
    Task<Team> CreateAsync(Team team, CancellationToken cancellationToken = default);

    /// <summary>Gets a team by ID, including its members.</summary>
    Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets all teams a user belongs to (as owner or member).</summary>
    Task<IReadOnlyList<Team>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Gets all teams owned by a specific user.</summary>
    Task<IReadOnlyList<Team>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>Updates team details (name, description).</summary>
    Task<Team> UpdateAsync(Team team, CancellationToken cancellationToken = default);

    /// <summary>Deletes a team and all its member associations.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Adds a user to a team.</summary>
    Task AddMemberAsync(Guid teamId, TeamMember member, CancellationToken cancellationToken = default);

    /// <summary>Removes a user from a team.</summary>
    Task RemoveMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Checks if a user is a member of a specific team.</summary>
    Task<bool> IsMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Checks whether a team with the given name already exists for the owner.</summary>
    Task<bool> ExistsByNameAsync(Guid ownerId, string name, CancellationToken cancellationToken = default);
}
