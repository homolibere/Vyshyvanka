using Vyshyvanka.Core.Enums;
using Vyshyvanka.Engine.Credentials;

namespace Vyshyvanka.Tests.Unit;

public class CredentialValidatorTests
{
    // --- ApiKey ---

    [Fact]
    public void WhenApiKeyDataIsValidThenValidationSucceeds()
    {
        var data = new Dictionary<string, string> { ["apiKey"] = "sk-12345" };

        var result = CredentialValidator.Validate(CredentialType.ApiKey, data);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void WhenApiKeyIsMissingThenValidationFails()
    {
        var data = new Dictionary<string, string>();

        var result = CredentialValidator.Validate(CredentialType.ApiKey, data);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "REQUIRED_FIELD");
    }

    [Fact]
    public void WhenApiKeyIsEmptyThenValidationFails()
    {
        var data = new Dictionary<string, string> { ["apiKey"] = "" };

        var result = CredentialValidator.Validate(CredentialType.ApiKey, data);

        result.IsValid.Should().BeFalse();
    }

    // --- OAuth2 ---

    [Fact]
    public void WhenOAuth2DataIsValidThenValidationSucceeds()
    {
        var data = new Dictionary<string, string>
        {
            ["clientId"] = "my-client",
            ["clientSecret"] = "my-secret"
        };

        var result = CredentialValidator.Validate(CredentialType.OAuth2, data);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void WhenOAuth2ClientIdIsMissingThenValidationFails()
    {
        var data = new Dictionary<string, string> { ["clientSecret"] = "my-secret" };

        var result = CredentialValidator.Validate(CredentialType.OAuth2, data);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Path == "data.clientId");
    }

    [Fact]
    public void WhenOAuth2ClientSecretIsMissingThenValidationFails()
    {
        var data = new Dictionary<string, string> { ["clientId"] = "my-client" };

        var result = CredentialValidator.Validate(CredentialType.OAuth2, data);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Path == "data.clientSecret");
    }

    // --- BasicAuth ---

    [Fact]
    public void WhenBasicAuthDataIsValidThenValidationSucceeds()
    {
        var data = new Dictionary<string, string>
        {
            ["username"] = "admin",
            ["password"] = "secret"
        };

        var result = CredentialValidator.Validate(CredentialType.BasicAuth, data);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void WhenBasicAuthUsernameMissingThenValidationFails()
    {
        var data = new Dictionary<string, string> { ["password"] = "secret" };

        var result = CredentialValidator.Validate(CredentialType.BasicAuth, data);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Path == "data.username");
    }

    [Fact]
    public void WhenBasicAuthPasswordMissingThenValidationFails()
    {
        var data = new Dictionary<string, string> { ["username"] = "admin" };

        var result = CredentialValidator.Validate(CredentialType.BasicAuth, data);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Path == "data.password");
    }

    // --- CustomHeaders ---

    [Fact]
    public void WhenCustomHeadersDataIsValidThenValidationSucceeds()
    {
        var data = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer token123",
            ["X-Custom"] = "value"
        };

        var result = CredentialValidator.Validate(CredentialType.CustomHeaders, data);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void WhenCustomHeadersIsEmptyThenValidationFails()
    {
        var data = new Dictionary<string, string>();

        var result = CredentialValidator.Validate(CredentialType.CustomHeaders, data);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void WhenCustomHeadersHasEmptyKeyThenValidationFails()
    {
        var data = new Dictionary<string, string> { [""] = "value" };

        var result = CredentialValidator.Validate(CredentialType.CustomHeaders, data);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "INVALID_HEADER_NAME");
    }

    // --- Null data ---

    [Fact]
    public void WhenDataIsNullThenThrowsArgumentNullException()
    {
        var act = () => CredentialValidator.Validate(CredentialType.ApiKey, null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
