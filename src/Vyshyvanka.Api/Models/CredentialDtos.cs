using Vyshyvanka.Contracts.Credentials;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Api.Models;

public static class CredentialMappings
{
    public static CredentialResponse ToResponse(this Credential credential, IReadOnlyList<string>? storedFields = null) => new()
    {
        Id = credential.Id,
        Name = credential.Name,
        Type = credential.Type,
        CreatedAt = credential.CreatedAt,
        UpdatedAt = credential.UpdatedAt,
        StoredFields = storedFields?.ToList()
    };
}
