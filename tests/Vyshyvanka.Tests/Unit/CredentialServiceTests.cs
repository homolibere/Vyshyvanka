using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Engine.Credentials;

namespace Vyshyvanka.Tests.Unit;

public class CredentialServiceTests
{
    private readonly ICredentialRepository _repository = Substitute.For<ICredentialRepository>();
    private readonly ICredentialEncryption _encryption = Substitute.For<ICredentialEncryption>();
    private readonly CredentialService _sut;

    public CredentialServiceTests()
    {
        _sut = new CredentialService(_repository, _encryption);
    }

    [Fact]
    public async Task WhenCreatingValidCredentialThenPersistsEncryptedData()
    {
        var request = new CreateCredentialRequest
        {
            Name = "My API Key",
            Type = CredentialType.ApiKey,
            Data = new Dictionary<string, string> { ["apiKey"] = "sk-12345" },
            OwnerId = Guid.NewGuid()
        };

        _encryption.Encrypt(Arg.Any<string>()).Returns([1, 2, 3]);
        _repository.CreateAsync(Arg.Any<Credential>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Credential>());

        var result = await _sut.CreateAsync(request);

        result.Name.Should().Be("My API Key");
        result.Type.Should().Be(CredentialType.ApiKey);
        result.EncryptedData.Should().NotBeEmpty();
        _encryption.Received(1).Encrypt(Arg.Any<string>());
    }

    [Fact]
    public async Task WhenCreatingCredentialWithInvalidDataThenThrowsArgumentException()
    {
        var request = new CreateCredentialRequest
        {
            Name = "Bad Credential",
            Type = CredentialType.ApiKey,
            Data = new Dictionary<string, string>(), // Missing apiKey
            OwnerId = Guid.NewGuid()
        };

        var act = () => _sut.CreateAsync(request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task WhenGettingExistingCredentialThenReturnsIt()
    {
        var credentialId = Guid.NewGuid();
        var credential = new Credential
        {
            Id = credentialId,
            Name = "Test",
            Type = CredentialType.ApiKey
        };
        _repository.GetByIdAsync(credentialId, Arg.Any<CancellationToken>())
            .Returns(credential);

        var result = await _sut.GetAsync(credentialId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(credentialId);
    }

    [Fact]
    public async Task WhenGettingNonexistentCredentialThenReturnsNull()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Credential?)null);

        var result = await _sut.GetAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task WhenGettingDecryptedCredentialThenDecryptsData()
    {
        var credentialId = Guid.NewGuid();
        var credential = new Credential
        {
            Id = credentialId,
            Name = "Test",
            Type = CredentialType.ApiKey,
            EncryptedData = [1, 2, 3]
        };
        _repository.GetByIdAsync(credentialId, Arg.Any<CancellationToken>())
            .Returns(credential);
        _encryption.Decrypt(Arg.Any<byte[]>())
            .Returns("""{"apiKey":"sk-12345"}""");

        var result = await _sut.GetDecryptedAsync(credentialId);

        result.Should().NotBeNull();
        result!.Values.Should().ContainKey("apiKey");
        result.Values["apiKey"].Should().Be("sk-12345");
    }

    [Fact]
    public async Task WhenDeletingCredentialThenCallsRepository()
    {
        var credentialId = Guid.NewGuid();
        _repository.DeleteAsync(credentialId, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _sut.DeleteAsync(credentialId);

        result.Should().BeTrue();
        await _repository.Received(1).DeleteAsync(credentialId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenUpdatingCredentialWithNewDataThenReEncrypts()
    {
        var credentialId = Guid.NewGuid();
        var existing = new Credential
        {
            Id = credentialId,
            Name = "Old Name",
            Type = CredentialType.ApiKey,
            EncryptedData = [1, 2, 3]
        };
        _repository.GetByIdAsync(credentialId, Arg.Any<CancellationToken>())
            .Returns(existing);
        _encryption.Encrypt(Arg.Any<string>()).Returns([4, 5, 6]);
        _repository.UpdateAsync(Arg.Any<Credential>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Credential>());

        var request = new UpdateCredentialRequest
        {
            Name = "New Name",
            Data = new Dictionary<string, string> { ["apiKey"] = "sk-new" }
        };

        var result = await _sut.UpdateAsync(credentialId, request);

        result.Name.Should().Be("New Name");
        _encryption.Received(1).Encrypt(Arg.Any<string>());
    }

    [Fact]
    public void WhenValidatingCredentialDataThenDelegatesToValidator()
    {
        var data = new Dictionary<string, string> { ["apiKey"] = "sk-12345" };

        var result = _sut.ValidateCredentialData(CredentialType.ApiKey, data);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task WhenListingCredentialsThenReturnsForOwner()
    {
        var ownerId = Guid.NewGuid();
        var credentials = new List<Credential>
        {
            new() { Id = Guid.NewGuid(), Name = "Cred 1", OwnerId = ownerId },
            new() { Id = Guid.NewGuid(), Name = "Cred 2", OwnerId = ownerId }
        };
        _repository.GetByOwnerIdAsync(ownerId, Arg.Any<CancellationToken>())
            .Returns(credentials);

        var result = await _sut.ListAsync(ownerId);

        result.Should().HaveCount(2);
    }
}
