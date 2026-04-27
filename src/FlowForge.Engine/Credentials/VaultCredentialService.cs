using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using Microsoft.Extensions.Logging;

namespace FlowForge.Engine.Credentials;

/// <summary>
/// <see cref="ICredentialService"/> implementation that stores secret values in
/// HashiCorp Vault or OpenBao (KV v2), while keeping metadata (name, type, owner)
/// in the local database via <see cref="ICredentialRepository"/>.
/// </summary>
public class VaultCredentialService(
    ICredentialRepository repository,
    IVaultClient vaultClient,
    ILogger<VaultCredentialService> logger) : ICredentialService
{
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

        var now = DateTime.UtcNow;
        var credential = new Credential
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
            EncryptedData = [], // No local encryption — secrets live in Vault
            OwnerId = request.OwnerId,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Write secret to Vault
        await vaultClient.WriteSecretAsync(credential.Id.ToString(), request.Data, cancellationToken);

        // Persist metadata locally
        var created = await repository.CreateAsync(credential, cancellationToken);

        logger.LogInformation("Created credential {Id} ({Name}) with secret in Vault", created.Id, created.Name);
        return created;
    }

    public async Task<Credential?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await repository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<DecryptedCredential?> GetDecryptedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var credential = await repository.GetByIdAsync(id, cancellationToken);
        if (credential is null)
        {
            return null;
        }

        var values = await vaultClient.ReadSecretAsync(id.ToString(), cancellationToken);
        if (values is null)
        {
            logger.LogWarning("Credential {Id} exists in DB but not in Vault", id);
            return null;
        }

        return new DecryptedCredential
        {
            Id = credential.Id,
            Type = credential.Type,
            Values = values
        };
    }

    public async Task<Credential> UpdateAsync(Guid id, UpdateCredentialRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await repository.GetByIdAsync(id, cancellationToken)
                       ?? throw new InvalidOperationException($"Credential {id} not found");

        if (request.Data is not null)
        {
            var validation = ValidateCredentialData(existing.Type, request.Data);
            if (!validation.IsValid)
            {
                throw new ArgumentException(
                    $"Invalid credential data: {string.Join(", ", validation.Errors.Select(e => e.Message))}",
                    nameof(request));
            }

            // Update secret in Vault
            await vaultClient.WriteSecretAsync(id.ToString(), request.Data, cancellationToken);
        }

        var updated = existing with
        {
            Name = request.Name ?? existing.Name,
            UpdatedAt = DateTime.UtcNow
        };

        return await repository.UpdateAsync(updated, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Delete from Vault first
        await vaultClient.DeleteSecretAsync(id.ToString(), cancellationToken);

        // Then remove metadata from DB
        return await repository.DeleteAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<Credential>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await repository.GetByOwnerIdAsync(userId, cancellationToken);
    }

    public ValidationResult ValidateCredentialData(CredentialType type, Dictionary<string, string> data)
    {
        return CredentialValidator.Validate(type, data);
    }
}
