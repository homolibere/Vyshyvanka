using System.ComponentModel.DataAnnotations;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Api.Models;

/// <summary>
/// Request to create a new team.
/// </summary>
public record CreateTeamRequest
{
    /// <summary>Display name of the team.</summary>
    [Required(ErrorMessage = "Name is required")]
    [MinLength(1, ErrorMessage = "Name cannot be empty")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional description.</summary>
    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; init; }
}

/// <summary>
/// Request to update a team.
/// </summary>
public record UpdateTeamRequest
{
    /// <summary>Display name of the team.</summary>
    [Required(ErrorMessage = "Name is required")]
    [MinLength(1, ErrorMessage = "Name cannot be empty")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional description.</summary>
    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; init; }
}

/// <summary>
/// Request to add a member to a team.
/// </summary>
public record AddTeamMemberRequest
{
    /// <summary>User ID to add.</summary>
    [Required]
    public Guid UserId { get; init; }

    /// <summary>Role within the team.</summary>
    public TeamRole Role { get; init; } = TeamRole.Member;
}

/// <summary>
/// Team API response.
/// </summary>
public record TeamResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid OwnerId { get; init; }
    public List<TeamMemberResponse> Members { get; init; } = [];
    public DateTime CreatedAt { get; init; }

    public static TeamResponse FromModel(Team team) => new()
    {
        Id = team.Id,
        Name = team.Name,
        Description = team.Description,
        OwnerId = team.OwnerId,
        Members = team.Members.Select(TeamMemberResponse.FromModel).ToList(),
        CreatedAt = team.CreatedAt
    };
}

/// <summary>
/// Team member API response.
/// </summary>
public record TeamMemberResponse
{
    public Guid UserId { get; init; }
    public string? DisplayName { get; init; }
    public string? Email { get; init; }
    public TeamRole Role { get; init; }
    public DateTime JoinedAt { get; init; }

    public static TeamMemberResponse FromModel(TeamMember member) => new()
    {
        UserId = member.UserId,
        DisplayName = member.DisplayName,
        Email = member.Email,
        Role = member.Role,
        JoinedAt = member.JoinedAt
    };
}
