using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Vyshyvanka.Core.Models;

/// <summary>
/// Represents a team — a named group of users for sharing workflows collectively.
/// </summary>
public record Team
{
    /// <summary>Unique identifier for the team.</summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    /// <summary>Display name of the team.</summary>
    [JsonPropertyName("name")]
    [Required(ErrorMessage = "Team name is required")]
    [MinLength(1, ErrorMessage = "Team name cannot be empty")]
    [MaxLength(100, ErrorMessage = "Team name cannot exceed 100 characters")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional description of the team's purpose.</summary>
    [JsonPropertyName("description")]
    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; init; }

    /// <summary>User who created and owns the team.</summary>
    [JsonPropertyName("ownerId")]
    public Guid OwnerId { get; init; }

    /// <summary>Team members.</summary>
    [JsonPropertyName("members")]
    public List<TeamMember> Members { get; init; } = [];

    /// <summary>When the team was created.</summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }
}
