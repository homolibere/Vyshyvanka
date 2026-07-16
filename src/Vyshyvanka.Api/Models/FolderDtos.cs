using System.ComponentModel.DataAnnotations;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Api.Models;

/// <summary>
/// Request to create a new folder.
/// </summary>
public record CreateFolderRequest
{
    /// <summary>Display name of the folder.</summary>
    [Required(ErrorMessage = "Name is required")]
    [MinLength(1, ErrorMessage = "Name cannot be empty")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional color hex code for visual differentiation.</summary>
    [MaxLength(7, ErrorMessage = "Color must be a hex code (e.g. #FF5733)")]
    public string? Color { get; init; }
}

/// <summary>
/// Request to update a folder.
/// </summary>
public record UpdateFolderRequest
{
    /// <summary>Display name of the folder.</summary>
    [Required(ErrorMessage = "Name is required")]
    [MinLength(1, ErrorMessage = "Name cannot be empty")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional color hex code.</summary>
    [MaxLength(7, ErrorMessage = "Color must be a hex code (e.g. #FF5733)")]
    public string? Color { get; init; }
}

/// <summary>
/// Folder API response.
/// </summary>
public record FolderResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Color { get; init; }
    public Guid OwnerId { get; init; }
    public int WorkflowCount { get; init; }
    public DateTime CreatedAt { get; init; }

    public static FolderResponse FromModel(Folder folder) => new()
    {
        Id = folder.Id,
        Name = folder.Name,
        Color = folder.Color,
        OwnerId = folder.OwnerId,
        WorkflowCount = folder.WorkflowCount,
        CreatedAt = folder.CreatedAt
    };
}

/// <summary>
/// Request to move a workflow to a folder (or to root).
/// </summary>
public record MoveToFolderRequest
{
    /// <summary>Target folder ID. Null moves the workflow to root (unfiled).</summary>
    public Guid? FolderId { get; init; }
}
