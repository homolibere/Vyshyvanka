using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Engine.Credentials;

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
    public async Task<Credential> CreateAsync(CreateCredentialRequest request,
        CancellationToken cancellationToken = default)
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
    public async Task<Credential> UpdateAsync(Guid id, UpdateCredentialRequest request,
        CancellationToken cancellationToken = default)
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
        return CredentialValidator.Validate(type, data);
    }
}
