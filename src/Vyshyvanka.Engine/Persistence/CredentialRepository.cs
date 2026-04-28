using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Engine.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Vyshyvanka.Engine.Persistence;

/// <summary>
/// EF Core implementation of credential repository.
/// </summary>
public class CredentialRepository : ICredentialRepository
{
    private readonly VyshyvankaDbContext _context;

    public CredentialRepository(VyshyvankaDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task<Credential> CreateAsync(Credential credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        var entity = ToEntity(credential);
        _context.Credentials.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        
        return ToModel(entity);
    }

    /// <inheritdoc />
    public async Task<Credential?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Credentials
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        
        return entity is null ? null : ToModel(entity);
    }

    /// <inheritdoc />
    public async Task<Credential> UpdateAsync(Credential credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        var entity = await _context.Credentials
            .FirstOrDefaultAsync(c => c.Id == credential.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Credential {credential.Id} not found");

        entity.Name = credential.Name;
        entity.Type = credential.Type;
        entity.EncryptedData = credential.EncryptedData;
        entity.UpdatedAt = credential.UpdatedAt;

        await _context.SaveChangesAsync(cancellationToken);
        
        return ToModel(entity);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Credentials.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _context.Credentials.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Credential>> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.Credentials
            .Where(c => c.OwnerId == ownerId)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
        
        return entities.Select(ToModel).ToList();
    }

    private static CredentialEntity ToEntity(Credential credential)
    {
        return new CredentialEntity
        {
            Id = credential.Id,
            Name = credential.Name,
            Type = credential.Type,
            EncryptedData = credential.EncryptedData,
            OwnerId = credential.OwnerId,
            CreatedAt = credential.CreatedAt,
            UpdatedAt = credential.UpdatedAt
        };
    }

    private static Credential ToModel(CredentialEntity entity)
    {
        return new Credential
        {
            Id = entity.Id,
            Name = entity.Name,
            Type = entity.Type,
            EncryptedData = entity.EncryptedData,
            OwnerId = entity.OwnerId,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
