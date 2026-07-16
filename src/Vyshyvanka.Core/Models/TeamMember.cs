using System.Text.Json.Serialization;
using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Core.Models;

/// <summary>
/// Represents a user's membership in a team.
/// </summary>
public record TeamMember
{
    /// <summary>The user's ID.</summary>
    [JsonPropertyName("userId")]
    public Guid UserId { get; init; }

    /// <summary>The user's display name (populated from User record).</summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    /// <summary>The user's email (populated from User record).</summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    /// <summary>Role of the user within this team.</summary>
    [JsonPropertyName("role")]
    public TeamRole Role { get; init; }

    /// <summary>When the user joined the team.</summary>
    [JsonPropertyName("joinedAt")]
    public DateTime JoinedAt { get; init; }
}
