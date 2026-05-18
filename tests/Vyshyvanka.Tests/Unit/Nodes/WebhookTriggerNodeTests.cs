using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Credentials;
using ExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;
using Vyshyvanka.Engine.Nodes.Triggers;

namespace Vyshyvanka.Tests.Unit.Nodes;

public class WebhookTriggerNodeTests
{
    private readonly WebhookTriggerNode _sut = new();

    private static ExecutionContext CreateContext() =>
        new(Guid.NewGuid(), Guid.NewGuid(), NullCredentialProvider.Instance);

    [Fact]
    public void WhenCreatedThenHasCorrectMetadata()
    {
        _sut.Type.Should().Be("webhook-trigger");
        _sut.Category.Should().Be(NodeCategory.Trigger);
        _sut.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task WhenTriggerDataIsUndefinedThenShouldNotTrigger()
    {
        var context = new TriggerContext { TriggerData = default };

        var result = await _sut.ShouldTriggerAsync(context);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task WhenTriggerDataIsNullThenShouldNotTrigger()
    {
        var context = new TriggerContext
        {
            TriggerData = JsonSerializer.SerializeToElement<object?>(null)
        };

        var result = await _sut.ShouldTriggerAsync(context);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task WhenTriggerTypeIsWebhookThenShouldTrigger()
    {
        var context = new TriggerContext
        {
            TriggerData = JsonSerializer.SerializeToElement(new { triggerType = "webhook" })
        };

        var result = await _sut.ShouldTriggerAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task WhenTriggerTypeIsNotWebhookThenShouldNotTrigger()
    {
        var context = new TriggerContext
        {
            TriggerData = JsonSerializer.SerializeToElement(new { triggerType = "schedule" })
        };

        var result = await _sut.ShouldTriggerAsync(context);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task WhenTriggerTypeIsMissingThenShouldNotTrigger()
    {
        var context = new TriggerContext
        {
            TriggerData = JsonSerializer.SerializeToElement(new { someOtherField = "value" })
        };

        var result = await _sut.ShouldTriggerAsync(context);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task WhenExecutedWithFullWebhookDataThenExtractsAllProperties()
    {
        var webhookPayload = JsonSerializer.SerializeToElement(new
        {
            headers = new { contentType = "application/json", authorization = "Bearer token" },
            body = new { name = "test", value = 42 },
            query = new { page = "1" },
            method = "POST",
            path = "/api/webhook/123"
        });
        var input = new NodeInput
        {
            Data = webhookPayload,
            Configuration = default
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("method").GetString().Should().Be("POST");
        result.Data.GetProperty("path").GetString().Should().Be("/api/webhook/123");
        result.Data.TryGetProperty("headers", out _).Should().BeTrue();
        result.Data.TryGetProperty("body", out _).Should().BeTrue();
        result.Data.TryGetProperty("query", out _).Should().BeTrue();
        result.Data.TryGetProperty("timestamp", out _).Should().BeTrue();
    }

    [Fact]
    public async Task WhenExecutedWithPartialDataThenMissingPropertiesAreNull()
    {
        var webhookPayload = JsonSerializer.SerializeToElement(new
        {
            method = "GET",
            path = "/webhook"
        });
        var input = new NodeInput
        {
            Data = webhookPayload,
            Configuration = default
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("method").GetString().Should().Be("GET");
        result.Data.GetProperty("path").GetString().Should().Be("/webhook");
        result.Data.GetProperty("headers").ValueKind.Should().Be(JsonValueKind.Null);
        result.Data.GetProperty("body").ValueKind.Should().Be(JsonValueKind.Null);
        result.Data.GetProperty("query").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task WhenExecutedWithNonObjectDataThenPropertiesAreNull()
    {
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement("just a string"),
            Configuration = default
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("headers").ValueKind.Should().Be(JsonValueKind.Null);
        result.Data.GetProperty("body").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task WhenExecutedWithBooleanPropertyThenExtractsCorrectly()
    {
        var webhookPayload = JsonSerializer.SerializeToElement(new
        {
            method = "POST",
            path = "/hook",
            body = true
        });
        var input = new NodeInput
        {
            Data = webhookPayload,
            Configuration = default
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task WhenExecutedWithNumericPropertyThenExtractsCorrectly()
    {
        var webhookPayload = JsonSerializer.SerializeToElement(new
        {
            method = "POST",
            path = "/hook",
            body = 42.5m
        });
        var input = new NodeInput
        {
            Data = webhookPayload,
            Configuration = default
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
    }
}
