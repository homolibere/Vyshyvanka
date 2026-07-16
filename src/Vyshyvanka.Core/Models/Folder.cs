using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Vyshyvanka.Core.Models;

/// <summary>
/// Represents a flat folder for organizing workflows.
/// </summary>
public record Folder
{
    /// <summary>Unique identifier for the folder.</summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    /// <summary>Display name of the folder.</summary>
    [JsonPropertyName("name")]
    [Required(ErrorMessage = "Folder name is required")]
    [MinLength(1, ErrorMessage = "Folder name cannot be empty")]
    [MaxLength(100, ErrorMessage = "Folder name cannot exceed 100 characters")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional color for visual differentiation in the UI (hex code).</summary>
    [JsonPropertyName("color")]
    [MaxLength(7, ErrorMessage = "Color must be a hex code (e.g. #FF5733)")]
    public string? Color { get; init; }

    /// <summary>User who owns this folder.</summary>
    [JsonPropertyName("ownerId")]
    public Guid OwnerId { get; init; }

    /// <summary>Number of workflows in this folder (populated by queries).</summary>
    [JsonPropertyName("workflowCount")]
    public int WorkflowCount { get; init; }

    /// <summary>When the folder was created.</summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }
}
