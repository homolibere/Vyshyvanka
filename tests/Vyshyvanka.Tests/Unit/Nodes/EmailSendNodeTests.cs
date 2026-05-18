using System.Net.Mail;
using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Credentials;
using Vyshyvanka.Engine.Nodes.Actions;
using ExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;

namespace Vyshyvanka.Tests.Unit.Nodes;

public class EmailSendNodeTests
{
    private static ExecutionContext CreateContext() =>
        new(Guid.NewGuid(), Guid.NewGuid(), NullCredentialProvider.Instance);

    [Fact]
    public void WhenCreatedThenHasCorrectMetadata()
    {
        var sut = new EmailSendNode();
        sut.Type.Should().Be("email-send");
        sut.Category.Should().Be(NodeCategory.Action);
        sut.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task WhenSmtpHostMissingAndNoCredentialsThenReturnsFailure()
    {
        var sut = new EmailSendNode(() => new SmtpClient());
        var config = JsonSerializer.SerializeToElement(new
        {
            to = "recipient@example.com",
            subject = "Test",
            body = "Test body"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("SMTP host is required");
    }

    [Fact]
    public async Task WhenSenderMissingAndNoCredentialsThenReturnsFailure()
    {
        var sut = new EmailSendNode(() => new SmtpClient());
        var config = JsonSerializer.SerializeToElement(new
        {
            to = "recipient@example.com",
            subject = "Test",
            body = "Test body",
            smtpHost = "smtp.example.com"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Sender email is required");
    }

    [Fact]
    public async Task WhenToIsMissingThenReturnsFailure()
    {
        var sut = new EmailSendNode(() => new SmtpClient());
        var config = JsonSerializer.SerializeToElement(new
        {
            subject = "Test",
            body = "Test body",
            from = "sender@example.com",
            smtpHost = "smtp.example.com"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task WhenSmtpFactoryThrowsSmtpExceptionThenReturnsSmtpError()
    {
        var sut = new EmailSendNode(() => throw new SmtpException("Connection refused"));
        var config = JsonSerializer.SerializeToElement(new
        {
            to = "recipient@example.com",
            subject = "Test",
            body = "Test body",
            from = "sender@example.com",
            smtpHost = "smtp.example.com"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("SMTP error");
    }

    [Fact]
    public async Task WhenSmtpFactoryThrowsGenericExceptionThenReturnsEmailSendError()
    {
        var sut = new EmailSendNode(() => throw new InvalidOperationException("Something broke"));
        var config = JsonSerializer.SerializeToElement(new
        {
            to = "recipient@example.com",
            subject = "Test",
            body = "Test body",
            from = "sender@example.com",
            smtpHost = "smtp.example.com"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Email send error");
    }

    [Fact]
    public async Task WhenCredentialsProvidedWithHostThenUsesCredentialHost()
    {
        // This test verifies that credentials are fetched and used for SMTP settings
        // The actual send will fail (no real SMTP server) but we verify the error path
        var credentialId = Guid.NewGuid();
        var credentialProvider = Substitute.For<ICredentialProvider>();
        credentialProvider.GetCredentialAsync(credentialId, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string>
            {
                ["host"] = "smtp.credentials.com",
                ["port"] = "465",
                ["username"] = "user@credentials.com",
                ["password"] = "secret"
            });

        var context = new ExecutionContext(
            Guid.NewGuid(), Guid.NewGuid(), credentialProvider);

        // Use a factory that throws to verify we get past credential resolution
        var sut = new EmailSendNode(() => throw new SmtpException("Expected failure after credential resolution"));
        var config = JsonSerializer.SerializeToElement(new
        {
            to = "recipient@example.com",
            subject = "Credential Test",
            body = "Test body"
        });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config,
            CredentialId = credentialId
        };

        var result = await sut.ExecuteAsync(input, context);

        // The fact that we get an SMTP error (not "host is required" or "sender is required")
        // proves that credentials were successfully resolved and applied
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("SMTP error");
        await credentialProvider.Received(1).GetCredentialAsync(credentialId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenSubjectIsMissingThenReturnsFailure()
    {
        var sut = new EmailSendNode(() => new SmtpClient());
        var config = JsonSerializer.SerializeToElement(new
        {
            to = "recipient@example.com",
            body = "Test body",
            from = "sender@example.com",
            smtpHost = "smtp.example.com"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task WhenBodyIsMissingThenReturnsFailure()
    {
        var sut = new EmailSendNode(() => new SmtpClient());
        var config = JsonSerializer.SerializeToElement(new
        {
            to = "recipient@example.com",
            subject = "Test",
            from = "sender@example.com",
            smtpHost = "smtp.example.com"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
    }
}
