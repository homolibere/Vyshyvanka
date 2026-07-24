namespace Vyshyvanka.Contracts.Folders;

/// <summary>
/// Request to create a new folder.
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
}

/// <summary>
/// Request to move a workflow to a folder (or to root).
/// </summary>
public record MoveToFolderRequest
{
    /// <summary>Target folder ID. Null moves the workflow to root (unfiled).</summary>
    public Guid? FolderId { get; init; }
}
