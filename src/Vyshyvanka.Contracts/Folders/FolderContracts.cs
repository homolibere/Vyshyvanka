namespace Vyshyvanka.Contracts.Folders;

/// <summary>
/// Request payload to create a new folder for organizing workflows.
/// </summary>
public record CreateFolderRequest
{
    /// <summary>Display name of the folder. Must be unique per owner.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional color used to visually distinguish the folder in the UI. <c>null</c> for the default color.</summary>
    public string? Color { get; init; }
}

/// <summary>
/// Request payload to update a folder's details.
/// </summary>
public record UpdateFolderRequest
{
    /// <summary>Updated display name of the folder. Must be unique per owner.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Updated color. <c>null</c> resets to the default color.</summary>
    public string? Color { get; init; }
}

/// <summary>
/// Represents a folder that groups a user's workflows.
/// </summary>
public record FolderResponse
{
    /// <summary>Unique identifier of the folder.</summary>
    public Guid Id { get; init; }

    /// <summary>Display name of the folder.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Color used to distinguish the folder in the UI. <c>null</c> when using the default color.</summary>
    public string? Color { get; init; }

    /// <summary>Identifier of the user who owns the folder.</summary>
    public Guid OwnerId { get; init; }

    /// <summary>Number of workflows currently contained in the folder.</summary>
    public int WorkflowCount { get; init; }

    /// <summary>UTC timestamp when the folder was created.</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Request payload to move a workflow into a folder or back to the root.
/// </summary>
public record MoveToFolderRequest
{
    /// <summary>Target folder identifier. <c>null</c> moves the workflow to root (unfiled).</summary>
    public Guid? FolderId { get; init; }
}
