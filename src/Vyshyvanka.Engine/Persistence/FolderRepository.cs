using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Engine.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Vyshyvanka.Engine.Persistence;

/// <summary>
/// EF Core implementation of the folder repository.
/// </summary>
public class FolderRepository(VyshyvankaDbContext context) : IFolderRepository
{
    /// <inheritdoc />
    public async Task<Folder> CreateAsync(Folder folder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);

        var entity = ToEntity(folder);
        context.Folders.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return ToModel(entity, 0);
    }

    /// <inheritdoc />
    public async Task<Folder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Folders
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        if (entity is null)
            return null;

        var workflowCount = await context.Workflows
            .CountAsync(w => w.FolderId == id, cancellationToken);

        return ToModel(entity, workflowCount);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Folder>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var entities = await context.Folders
            .AsNoTracking()
            .Where(f => f.OwnerId == ownerId)
            .OrderBy(f => f.Name)
            .ToListAsync(cancellationToken);

        var folderIds = entities.Select(f => f.Id).ToList();

        var workflowCounts = await context.Workflows
            .Where(w => w.FolderId != null && folderIds.Contains(w.FolderId.Value))
            .GroupBy(w => w.FolderId!.Value)
            .Select(g => new { FolderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FolderId, x => x.Count, cancellationToken);

        return entities
            .Select(e => ToModel(e, workflowCounts.GetValueOrDefault(e.Id, 0)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<Folder> UpdateAsync(Folder folder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);

        var entity = await context.Folders
            .FirstOrDefaultAsync(f => f.Id == folder.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Folder {folder.Id} not found");

        entity.Name = folder.Name;
        entity.Color = folder.Color;

        await context.SaveChangesAsync(cancellationToken);

        var workflowCount = await context.Workflows
            .CountAsync(w => w.FolderId == folder.Id, cancellationToken);

        return ToModel(entity, workflowCount);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Folders.FindAsync([id], cancellationToken);
        if (entity is null)
            return false;

        // FolderId on workflows is set to null by cascade (SetNull), handled by EF/DB
        context.Folders.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByNameAsync(Guid ownerId, string name, CancellationToken cancellationToken = default)
    {
        return await context.Folders
            .AnyAsync(f => f.OwnerId == ownerId && f.Name == name, cancellationToken);
    }

    private static FolderEntity ToEntity(Folder folder) => new()
    {
        Id = folder.Id,
        Name = folder.Name,
        Color = folder.Color,
        OwnerId = folder.OwnerId,
        CreatedAt = folder.CreatedAt
    };

    private static Folder ToModel(FolderEntity entity, int workflowCount) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Color = entity.Color,
        OwnerId = entity.OwnerId,
        WorkflowCount = workflowCount,
        CreatedAt = entity.CreatedAt
    };
}
