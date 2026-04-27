using FlowForge.Core.Enums;

namespace FlowForge.Designer.Models;

/// <summary>
/// Client-side credential model (never contains secret values).
/// </summary>
public record CredentialModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public CredentialType Type { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Field keys that have stored values (populated on detail fetch, null on list).
    /// </summary>
    public List<string>? StoredFields { get; init; }
}

/// <summary>
/// Request to create a credential.
/// </summary>
public record CreateCredentialModel
{
    public string Name { get; init; } = string.Empty;
    public CredentialType Type { get; init; }
    public Dictionary<string, string> Data { get; init; } = [];
}

/// <summary>
/// Request to update a credential.
/// </summary>
public record UpdateCredentialModel
{
    public string? Name { get; init; }
    public Dictionary<string, string>? Data { get; init; }
}
