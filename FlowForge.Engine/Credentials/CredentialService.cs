using System.Text.Json;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;

namespace FlowForge.Engine.Credentials;

/// <summary>
/// Service for managing credentials with encryption.
/// </summary>
public class CredentialService : ICredentialService
{
    private readonly ICredentialRepository _repository;
    private readonly ICredentialEncryption _encryption;

    public CredentialService(ICredentialRepository repository, ICredentialEncryption encryption)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _encryption = encryption ?? throw new ArgumentNullException(nameof(encryption));
    }

    /// <inheritdoc />
    public async Task<Credential> CreateAsync(CreateCredentialRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        var validation = ValidateCredentialData(request.Type, request.Data);
        if (!validation.IsValid)
        {
            throw new ArgumentException(
                $"Invalid credential data: {string.Join(", ", validation.Errors.Select(e => e.Message))}",
                nameof(request));
        }
        
        var serializedData = JsonSerializer.Serialize(request.Data);
        var encryptedData = _encryption.Encrypt(serializedData);
        
        var now = DateTime.UtcNow;
        var credential = new Credential
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
            EncryptedData = encryptedData,
            OwnerId = request.OwnerId,
            CreatedAt = now,
            UpdatedAt = now
        };
        
        return await _repository.CreateAsync(credential, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Credential?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DecryptedCredential?> GetDecryptedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var credential = await _repository.GetByIdAsync(id, cancellationToken);
        if (credential is null)
        {
            return null;
        }
        
        var decryptedJson = _encryption.Decrypt(credential.EncryptedData);
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(decryptedJson) ?? [];
        
        return new DecryptedCredential
        {
            Id = credential.Id,
            Type = credential.Type,
            Values = values
        };
    }

    /// <inheritdoc />
    public async Task<Credential> UpdateAsync(Guid id, UpdateCredentialRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        var existing = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Credential {id} not found");
        
        byte[] encryptedData = existing.EncryptedData;
        
        if (request.Data is not null)
        {
            var validation = ValidateCredentialData(existing.Type, request.Data);
            if (!validation.IsValid)
            {
                throw new ArgumentException(
                    $"Invalid credential data: {string.Join(", ", validation.Errors.Select(e => e.Message))}",
                    nameof(request));
            }
            
            var serializedData = JsonSerializer.Serialize(request.Data);
            encryptedData = _encryption.Encrypt(serializedData);
        }
        
        var updated = existing with
        {
            Name = request.Name ?? existing.Name,
            EncryptedData = encryptedData,
            UpdatedAt = DateTime.UtcNow
        };
        
        return await _repository.UpdateAsync(updated, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _repository.DeleteAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Credential>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByOwnerIdAsync(userId, cancellationToken);
    }

    /// <inheritdoc />
    public ValidationResult ValidateCredentialData(CredentialType type, Dictionary<string, string> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        
        var errors = new List<ValidationError>();
        
        switch (type)
        {
            case CredentialType.ApiKey:
                ValidateApiKeyCredential(data, errors);
                break;
                
            case CredentialType.OAuth2:
                ValidateOAuth2Credential(data, errors);
                break;
                
            case CredentialType.BasicAuth:
                ValidateBasicAuthCredential(data, errors);
                break;
                
            case CredentialType.CustomHeaders:
                ValidateCustomHeadersCredential(data, errors);
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

    private static void ValidateApiKeyCredential(Dictionary<string, string> data, List<ValidationError> errors)
    {
        if (!data.TryGetValue("apiKey", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
        {
            errors.Add(new ValidationError
            {
                Path = "data.apiKey",
                Message = "API key is required",
                ErrorCode = "REQUIRED_FIELD"
            });
        }
    }

    private static void ValidateOAuth2Credential(Dictionary<string, string> data, List<ValidationError> errors)
    {
        if (!data.TryGetValue("clientId", out var clientId) || string.IsNullOrWhiteSpace(clientId))
        {
            errors.Add(new ValidationError
            {
                Path = "data.clientId",
                Message = "Client ID is required",
                ErrorCode = "REQUIRED_FIELD"
            });
        }
        
        if (!data.TryGetValue("clientSecret", out var clientSecret) || string.IsNullOrWhiteSpace(clientSecret))
        {
            errors.Add(new ValidationError
            {
                Path = "data.clientSecret",
                Message = "Client secret is required",
                ErrorCode = "REQUIRED_FIELD"
            });
        }
    }

    private static void ValidateBasicAuthCredential(Dictionary<string, string> data, List<ValidationError> errors)
    {
        if (!data.TryGetValue("username", out var username) || string.IsNullOrWhiteSpace(username))
        {
            errors.Add(new ValidationError
            {
                Path = "data.username",
                Message = "Username is required",
                ErrorCode = "REQUIRED_FIELD"
            });
        }
        
        if (!data.TryGetValue("password", out var password) || string.IsNullOrWhiteSpace(password))
        {
            errors.Add(new ValidationError
            {
                Path = "data.password",
                Message = "Password is required",
                ErrorCode = "REQUIRED_FIELD"
            });
        }
    }

    private static void ValidateCustomHeadersCredential(Dictionary<string, string> data, List<ValidationError> errors)
    {
        if (data.Count == 0)
        {
            errors.Add(new ValidationError
            {
                Path = "data",
                Message = "At least one header is required",
                ErrorCode = "REQUIRED_FIELD"
            });
        }
        
        foreach (var (key, value) in data)
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
    }
}
