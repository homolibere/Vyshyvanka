using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Designer.Models;

/// <summary>
/// Team response from the API.
/// </summary>
public record TeamResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid OwnerId { get; init; }
    public List<TeamMemberResponse> Members { get; init; } = [];
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Team member response.
/// </summary>
public record TeamMemberResponse
{
    public Guid UserId { get; init; }
    public string? DisplayName { get; init; }
    public string? Email { get; init; }
    public TeamRole Role { get; init; }
    public DateTime JoinedAt { get; init; }
}

/// <summary>
/// Request to create a team.
/// </summary>
public record CreateTeamRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

/// <summary>
/// Request to update a team.
/// </summary>
public record UpdateTeamRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

/// <summary>
/// Request to add a member to a team.
/// </summary>
public record AddTeamMemberRequest
{
    public Guid UserId { get; init; }
    public TeamRole Role { get; init; } = TeamRole.Member;
}
