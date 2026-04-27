using FlowForge.Core.Enums;
using FlowForge.Core.Models;

namespace FlowForge.Engine.Credentials;

/// <summary>
/// Shared validation logic for credential data, used by both
/// <see cref="CredentialService"/> and <see cref="VaultCredentialService"/>.
/// </summary>
public static class CredentialValidator
{
    public static ValidationResult Validate(CredentialType type, Dictionary<string, string> data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var errors = new List<ValidationError>();

        switch (type)
        {
            case CredentialType.ApiKey:
                RequireField(data, "apiKey", "API key", errors);
                break;

            case CredentialType.OAuth2:
                RequireField(data, "clientId", "Client ID", errors);
                RequireField(data, "clientSecret", "Client secret", errors);
                break;

            case CredentialType.BasicAuth:
                RequireField(data, "username", "Username", errors);
                RequireField(data, "password", "Password", errors);
                break;

            case CredentialType.CustomHeaders:
                if (data.Count == 0)
                {
                    errors.Add(new ValidationError
                    {
                        Path = "data",
                        Message = "At least one header is required",
                        ErrorCode = "REQUIRED_FIELD"
                    });
                }

                foreach (var (key, _) in data)
                {
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        errors.Add(new ValidationError
                        {
                            Path = "data",
                            Message = "Header name cannot be empty",
                            ErrorCode = "INVALID_HEADER_NAME"
                        });
                    }
                }

                break;

            default:
                errors.Add(new ValidationError
                {
                    Path = "type",
                    Message = $"Unknown credential type: {type}",
                    ErrorCode = "UNKNOWN_CREDENTIAL_TYPE"
                });
                break;
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    private static void RequireField(
        Dictionary<string, string> data,
        string field,
        string displayName,
        List<ValidationError> errors)
    {
        if (!data.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ValidationError
            {
                Path = $"data.{field}",
                Message = $"{displayName} is required",
                ErrorCode = "REQUIRED_FIELD"
            });
        }
    }
}
