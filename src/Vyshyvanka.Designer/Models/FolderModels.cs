namespace Vyshyvanka.Designer.Models;

/// <summary>
/// Folder response from the API.
/// </summary>
public record FolderResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Color { get; init; }
    public Guid OwnerId { get; init; }
    public int WorkflowCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Request to create a folder.
/// </summary>
public record CreateFolderRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Color { get; init; }
}

/// <summary>
/// Request to update a folder.
/// </summary>
public record UpdateFolderRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Color { get; init; }
}
