using FlowForge.Core.Enums;

namespace FlowForge.Core.Models;

/// <summary>
/// Represents a user in the system.
/// </summary>
public record User
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string PasswordHash { get; init; } = string.Empty;
    public UserRole Role { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
}
