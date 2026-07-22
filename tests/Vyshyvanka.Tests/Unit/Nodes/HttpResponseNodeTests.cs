using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Credentials;
using Vyshyvanka.Engine.Execution;
using Vyshyvanka.Engine.Nodes.Actions;
using ExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;

namespace Vyshyvanka.Tests.Unit.Nodes;

public class HttpResponseNodeTests
{
    private readonly HttpResponseNode _sut = new();

    private static ExecutionContext CreateContext(IWebhookResponseWriter? webhookResponse = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), NullCredentialProvider.Instance)
        {
            WebhookResponse = webhookResponse
        };

    [Fact]
    public void WhenCreatedThenHasCorrectMetadata()
    {
        _sut.Type.Should().Be("http-response");
        _sut.Category.Should().Be(NodeCategory.Action);
        _sut.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task WhenWebhookResponseIsNullThenReturnsFailure()
    {
        var context = CreateContext(webhookResponse: null);
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new { message = "hello" }),
            Configuration = JsonSerializer.SerializeToElement(new { statusCode = 200 })
        };

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Webhook Trigger");
        result.ErrorMessage.Should().Contain("lastNode");
    }

    [Fact]
    public async Task WhenExecutedWithValidContextThenWritesResponseAndReturnsSuccess()
    {
        var writer = new WebhookResponseWriter();
        var context = CreateContext(writer);
        var config = JsonSerializer.SerializeToElement(new
        {
            statusCode = 201,
            contentType = "application/json",
            body = """{"id": 42}"""
        });
        var input = new NodeInput { Data = default, Configuration = config };

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("responseSent").GetBoolean().Should().BeTrue();
        result.Data.GetProperty("statusCode").GetInt32().Should().Be(201);
        result.Data.GetProperty("bodyLength").GetInt32().Should().Be("""{"id": 42}""".Length);

        // Verify the writer received the data
        var responseData = await writer.WaitForResponseAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        responseData.Should().NotBeNull();
        responseData!.StatusCode.Should().Be(201);
        responseData.Body.Should().Be("""{"id": 42}""");
        responseData.Headers.Should().ContainKey("Content-Type");
        responseData.Headers!["Content-Type"].Should().Be("application/json");
    }

    [Fact]
    public async Task WhenNoBodyConfiguredThenSerializesInputDataAsBody()
    {
        var writer = new WebhookResponseWriter();
        var context = CreateContext(writer);
        var inputData = JsonSerializer.SerializeToElement(new { name = "test", value = 123 });
        var config = JsonSerializer.SerializeToElement(new { statusCode = 200 });
        var input = new NodeInput { Data = inputData, Configuration = config };

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        var responseData = await writer.WaitForResponseAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        responseData!.Body.Should().Contain("\"name\"");
        responseData.Body.Should().Contain("\"test\"");
        responseData.Body.Should().Contain("123");
    }

    [Fact]
    public async Task WhenNoStatusCodeConfiguredThenDefaults200()
    {
        var writer = new WebhookResponseWriter();
        var context = CreateContext(writer);
        var config = JsonSerializer.SerializeToElement(new { body = "ok" });
        var input = new NodeInput { Data = default, Configuration = config };

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        var responseData = await writer.WaitForResponseAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        responseData!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task WhenCustomHeadersConfiguredThenIncludedInResponse()
    {
        var writer = new WebhookResponseWriter();
        var context = CreateContext(writer);
        var config = JsonSerializer.SerializeToElement(new
        {
            statusCode = 302,
            headers = new { Location = "https://example.com/redirect" },
            body = ""
        });
        var input = new NodeInput { Data = default, Configuration = config };

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        var responseData = await writer.WaitForResponseAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        responseData!.StatusCode.Should().Be(302);
        responseData.Headers.Should().ContainKey("Location");
        responseData.Headers!["Location"].Should().Be("https://example.com/redirect");
    }

    [Fact]
    public async Task WhenResponseAlreadySentThenReturnsFailure()
    {
        var writer = new WebhookResponseWriter();
        await writer.WriteAsync(200, null, "first", CancellationToken.None);

        var context = CreateContext(writer);
        var config = JsonSerializer.SerializeToElement(new { statusCode = 200, body = "second" });
        var input = new NodeInput { Data = default, Configuration = config };

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already been sent");
    }

    [Fact]
    public async Task WhenContentTypeNotSpecifiedThenDefaultsToApplicationJson()
    {
        var writer = new WebhookResponseWriter();
        var context = CreateContext(writer);
        var config = JsonSerializer.SerializeToElement(new { body = "{}" });
        var input = new NodeInput { Data = default, Configuration = config };

        await _sut.ExecuteAsync(input, context);

        var responseData = await writer.WaitForResponseAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        responseData!.Headers.Should().ContainKey("Content-Type");
        responseData.Headers!["Content-Type"].Should().Be("application/json");
    }

    [Fact]
    public async Task WhenContentTypeIsTextPlainThenUsesIt()
    {
        var writer = new WebhookResponseWriter();
        var context = CreateContext(writer);
        var config = JsonSerializer.SerializeToElement(new
        {
            contentType = "text/plain",
            body = "Hello world"
        });
        var input = new NodeInput { Data = default, Configuration = config };

        await _sut.ExecuteAsync(input, context);

        var responseData = await writer.WaitForResponseAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        responseData!.Headers!["Content-Type"].Should().Be("text/plain");
        responseData.Body.Should().Be("Hello world");
    }

    [Fact]
    public async Task WhenInputDataIsNullAndNoBodyConfiguredThenBodyIsNull()
    {
        var writer = new WebhookResponseWriter();
        var context = CreateContext(writer);
        var config = JsonSerializer.SerializeToElement(new { statusCode = 204 });
        var input = new NodeInput { Data = default, Configuration = config };

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        var responseData = await writer.WaitForResponseAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        responseData!.StatusCode.Should().Be(204);
        responseData.Body.Should().BeNull();
    }
}
