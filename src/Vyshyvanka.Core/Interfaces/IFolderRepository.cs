using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Core.Interfaces;

/// <summary>
/// Repository for persisting and querying workflow folders.
/// </summary>
public interface IFolderRepository
{
    /// <summary>Creates a new folder.</summary>
    Task<Folder> CreateAsync(Folder folder, CancellationToken cancellationToken = default);

    /// <summary>Gets a folder by ID.</summary>
    Task<Folder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets all folders owned by a user.</summary>
    Task<IReadOnlyList<Folder>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>Updates a folder (name, color).</summary>
    Task<Folder> UpdateAsync(Folder folder, CancellationToken cancellationToken = default);

    /// <summary>Deletes a folder. Workflows in this folder are moved to root (FolderId = null).</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Checks whether a folder with the given name already exists for the owner.</summary>
    Task<bool> ExistsByNameAsync(Guid ownerId, string name, CancellationToken cancellationToken = default);
}
