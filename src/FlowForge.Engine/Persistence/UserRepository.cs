using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using FlowForge.Engine.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Engine.Persistence;

/// <summary>
/// EF Core implementation of user repository.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly FlowForgeDbContext _context;

    public UserRepository(FlowForgeDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        return entity is null ? null : ToModel(entity);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        return entity is null ? null : ToModel(entity);
    }

    public async Task<User?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.ExternalId == externalId, cancellationToken);

        return entity is null ? null : ToModel(entity);
    }

    public async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        var entity = ToEntity(user);
        _context.Users.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToModel(entity);
    }

    public async Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Users.FindAsync([user.Id], cancellationToken)
                     ?? throw new InvalidOperationException($"User {user.Id} not found");

        entity.Email = user.Email;
        entity.DisplayName = user.DisplayName;
        entity.PasswordHash = user.PasswordHash;
        entity.Role = user.Role;
        entity.IsActive = user.IsActive;
        entity.LastLoginAt = user.LastLoginAt;

        await _context.SaveChangesAsync(cancellationToken);
        return ToModel(entity);
    }

    public async Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities.Select(ToModel);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Users.FindAsync([id], cancellationToken);
        if (entity is not null)
        {
            _context.Users.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    internal async Task UpdateRefreshTokenAsync(Guid userId, string? refreshToken, DateTime? expiresAt,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.Users.FindAsync([userId], cancellationToken)
                     ?? throw new InvalidOperationException($"User {userId} not found");

        entity.RefreshToken = refreshToken;
        entity.RefreshTokenExpiresAt = expiresAt;
        await _context.SaveChangesAsync(cancellationToken);
    }

    internal async Task<(string? RefreshToken, DateTime? ExpiresAt)> GetRefreshTokenAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return entity is null ? (null, null) : (entity.RefreshToken, entity.RefreshTokenExpiresAt);
    }

    internal async Task<User?> GetByRefreshTokenAsync(string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken, cancellationToken);

        return entity is null ? null : ToModel(entity);
    }

    private static User ToModel(UserEntity entity) => new()
    {
        Id = entity.Id,
        Email = entity.Email,
        DisplayName = entity.DisplayName,
        PasswordHash = entity.PasswordHash,
        Role = entity.Role,
        IsActive = entity.IsActive,
        CreatedAt = entity.CreatedAt,
        LastLoginAt = entity.LastLoginAt,
        ExternalId = entity.ExternalId,
        AuthenticationProvider = entity.AuthenticationProvider
    };

    private static UserEntity ToEntity(User model) => new()
    {
        Id = model.Id,
        Email = model.Email,
        DisplayName = model.DisplayName,
        PasswordHash = model.PasswordHash,
        Role = model.Role,
        IsActive = model.IsActive,
        CreatedAt = model.CreatedAt,
        LastLoginAt = model.LastLoginAt,
        ExternalId = model.ExternalId,
        AuthenticationProvider = model.AuthenticationProvider
    };
}
