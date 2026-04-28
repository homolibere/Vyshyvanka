namespace Vyshyvanka.Core.Models;

/// <summary>
/// Represents an API key for programmatic access.
/// </summary>
public record ApiKey
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string KeyHash { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public List<string> Scopes { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime? LastUsedAt { get; init; }
    public bool IsActive { get; init; } = true;
}
