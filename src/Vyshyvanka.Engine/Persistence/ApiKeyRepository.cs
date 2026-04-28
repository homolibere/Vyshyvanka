using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Engine.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Vyshyvanka.Engine.Persistence;

/// <summary>
/// EF Core implementation of API key repository.
/// </summary>
public class ApiKeyRepository : IApiKeyRepository
{
    private readonly VyshyvankaDbContext _context;

    public ApiKeyRepository(VyshyvankaDbContext context)
    {
        _context = context;
    }

    public async Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == id, cancellationToken);
        
        return entity is null ? null : ToModel(entity);
    }

    public async Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash, cancellationToken);
        
        return entity is null ? null : ToModel(entity);
    }

    public async Task<IEnumerable<ApiKey>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.ApiKeys
            .AsNoTracking()
            .Where(k => k.UserId == userId)
            .ToListAsync(cancellationToken);
        
        return entities.Select(ToModel);
    }

    public async Task<ApiKey> CreateAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        var entity = ToEntity(apiKey);
        _context.ApiKeys.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToModel(entity);
    }

    public async Task<ApiKey> UpdateAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ApiKeys.FindAsync([apiKey.Id], cancellationToken)
            ?? throw new InvalidOperationException($"API key {apiKey.Id} not found");
        
        entity.Name = apiKey.Name;
        entity.Scopes = string.Join(",", apiKey.Scopes);
        entity.ExpiresAt = apiKey.ExpiresAt;
        entity.LastUsedAt = apiKey.LastUsedAt;
        entity.IsActive = apiKey.IsActive;
        
        await _context.SaveChangesAsync(cancellationToken);
        return ToModel(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ApiKeys.FindAsync([id], cancellationToken);
        if (entity is not null)
        {
            _context.ApiKeys.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private static ApiKey ToModel(ApiKeyEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        KeyHash = entity.KeyHash,
        UserId = entity.UserId,
        Scopes = string.IsNullOrEmpty(entity.Scopes) 
            ? [] 
            : entity.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
        CreatedAt = entity.CreatedAt,
        ExpiresAt = entity.ExpiresAt,
        LastUsedAt = entity.LastUsedAt,
        IsActive = entity.IsActive
    };

    private static ApiKeyEntity ToEntity(ApiKey model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        KeyHash = model.KeyHash,
        UserId = model.UserId,
        Scopes = string.Join(",", model.Scopes),
        CreatedAt = model.CreatedAt,
        ExpiresAt = model.ExpiresAt,
        LastUsedAt = model.LastUsedAt,
        IsActive = model.IsActive
    };
}
