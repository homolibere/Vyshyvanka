using System.ComponentModel.DataAnnotations;
using FlowForge.Core.Enums;
using FlowForge.Core.Models;

namespace FlowForge.Api.Models;

/// <summary>
/// Request to create a new credential.
/// </summary>
public record CreateCredentialDto
{
    [Required(ErrorMessage = "Name is required")]
    [MinLength(1)]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    public CredentialType Type { get; init; }

    [Required(ErrorMessage = "Data is required")]
    public Dictionary<string, string> Data { get; init; } = [];
}

/// <summary>
/// Request to update an existing credential.
/// </summary>
public record UpdateCredentialDto
{
    [MaxLength(200)]
    public string? Name { get; init; }

    public Dictionary<string, string>? Data { get; init; }
}

/// <summary>
/// Credential response (never includes decrypted values).
/// </summary>
public record CredentialResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public CredentialType Type { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Field keys that have stored values (e.g. ["apiKey", "baseUrl"]).
    /// Values are never included — only the keys, so the UI can show masked indicators.
    /// </summary>
    public List<string>? StoredFields { get; init; }

    public static CredentialResponse FromModel(Credential credential, IReadOnlyList<string>? storedFields = null) => new()
    {
        Id = credential.Id,
        Name = credential.Name,
        Type = credential.Type,
        CreatedAt = credential.CreatedAt,
        UpdatedAt = credential.UpdatedAt,
        StoredFields = storedFields?.ToList()
    };
}
