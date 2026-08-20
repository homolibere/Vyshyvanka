using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Contracts.Teams;

/// <summary>
/// Request payload to create a team. The creator is automatically added as an owner.
/// </summary>
public record CreateTeamRequest
{
    /// <summary>Display name of the team. Required.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional free-text description of the team's purpose.</summary>
    public string? Description { get; init; }
}

/// <summary>
/// Request payload to update a team's details.
/// </summary>
public record UpdateTeamRequest
{
    /// <summary>Updated display name of the team.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Updated free-text description. <c>null</c> clears the description.</summary>
    public string? Description { get; init; }
}

/// <summary>
/// Request payload to add a member to a team.
/// </summary>
public record AddTeamMemberRequest
{
    /// <summary>Identifier of the user to add.</summary>
    public Guid UserId { get; init; }

    /// <summary>Role to assign the new member. Defaults to <see cref="TeamRole.Member"/>.</summary>
    public TeamRole Role { get; init; } = TeamRole.Member;
}

/// <summary>
/// Full representation of a team, including its members.
/// </summary>
public record TeamResponse
{
    /// <summary>Unique identifier of the team.</summary>
    public Guid Id { get; init; }

    /// <summary>Display name of the team.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional free-text description. <c>null</c> when unset.</summary>
    public string? Description { get; init; }

    /// <summary>Identifier of the user who owns the team.</summary>
    public Guid OwnerId { get; init; }

    /// <summary>The team's members and their roles.</summary>
    public List<TeamMemberResponse> Members { get; init; } = [];

    /// <summary>UTC timestamp when the team was created.</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Represents a single member of a team.
/// </summary>
public record TeamMemberResponse
{
    /// <summary>Identifier of the member user.</summary>
    public Guid UserId { get; init; }

    /// <summary>Display name of the member. <c>null</c> when unset.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Email of the member. <c>null</c> when unavailable.</summary>
    public string? Email { get; init; }

    /// <summary>The member's role within the team.</summary>
    public TeamRole Role { get; init; }

    /// <summary>UTC timestamp when the user joined the team.</summary>
    public DateTime JoinedAt { get; init; }
}
