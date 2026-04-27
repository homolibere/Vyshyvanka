namespace FlowForge.Designer.Models;

/// <summary>API key as returned by the list/get endpoints.</summary>
public record ApiKeyModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public List<string> Scopes { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime? LastUsedAt { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>Request to create a new API key.</summary>
public record CreateApiKeyModel
{
    public string Name { get; init; } = string.Empty;
    public List<string>? Scopes { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

/// <summary>Response after creating an API key (includes the plain-text key).</summary>
public record CreateApiKeyResponseModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public List<string> Scopes { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
}
