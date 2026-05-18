using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Credentials;
using ExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;
using Vyshyvanka.Engine.Nodes.Triggers;

namespace Vyshyvanka.Tests.Unit.Nodes;

public class ManualTriggerNodeTests
{
    private readonly ManualTriggerNode _sut = new();

    private static ExecutionContext CreateContext() =>
        new(Guid.NewGuid(), Guid.NewGuid(), NullCredentialProvider.Instance);

    [Fact]
    public void WhenCreatedThenHasCorrectMetadata()
    {
        _sut.Type.Should().Be("manual-trigger");
        _sut.Category.Should().Be(NodeCategory.Trigger);
        _sut.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task WhenShouldTriggerAsyncCalledThenAlwaysReturnsTrue()
    {
        var context = new TriggerContext { TriggerData = default };

        var result = await _sut.ShouldTriggerAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task WhenShouldTriggerAsyncCalledWithNullDataThenReturnsTrue()
    {
        var context = new TriggerContext
        {
            TriggerData = JsonSerializer.SerializeToElement<object?>(null)
        };

        var result = await _sut.ShouldTriggerAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task WhenExecutedWithRuntimeDataThenOutputsRuntimeData()
    {
        var runtimeData = JsonSerializer.SerializeToElement(new { message = "hello", count = 42 });
        var input = new NodeInput
        {
            Data = runtimeData,
            Configuration = default
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("message").GetString().Should().Be("hello");
        result.Data.GetProperty("count").GetInt32().Should().Be(42);
    }

    [Fact]
    public async Task WhenExecutedWithNoRuntimeDataButTestDataConfiguredThenOutputsTestData()
    {
        var testData = new { name = "test", value = 123 };
        var config = JsonSerializer.SerializeToElement(new { testData });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("name").GetString().Should().Be("test");
        result.Data.GetProperty("value").GetInt32().Should().Be(123);
    }

    [Fact]
    public async Task WhenExecutedWithNullRuntimeDataAndTestDataConfiguredThenOutputsTestData()
    {
        var testData = new { key = "value" };
        var config = JsonSerializer.SerializeToElement(new { testData });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement<object?>(null),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("key").GetString().Should().Be("value");
    }

    [Fact]
    public async Task WhenExecutedWithNoDataAndNoConfigThenOutputsDefaultPayload()
    {
        var input = new NodeInput
        {
            Data = default,
            Configuration = default
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("triggered").GetBoolean().Should().BeTrue();
        result.Data.TryGetProperty("timestamp", out _).Should().BeTrue();
    }

    [Fact]
    public async Task WhenExecutedWithRuntimeDataThenRuntimeDataTakesPriorityOverTestData()
    {
        var runtimeData = JsonSerializer.SerializeToElement(new { source = "runtime" });
        var config = JsonSerializer.SerializeToElement(new { testData = new { source = "config" } });
        var input = new NodeInput
        {
            Data = runtimeData,
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("source").GetString().Should().Be("runtime");
    }

    [Fact]
    public async Task WhenExecutedWithNullTestDataConfigThenOutputsDefaultPayload()
    {
        var config = JsonSerializer.SerializeToElement(new { testData = (object?)null });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("triggered").GetBoolean().Should().BeTrue();
    }
}
