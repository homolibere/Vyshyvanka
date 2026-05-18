using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Credentials;
using ExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;
using Vyshyvanka.Engine.Nodes.Triggers;

namespace Vyshyvanka.Tests.Unit.Nodes;

public class ScheduleTriggerNodeTests
{
    private readonly ScheduleTriggerNode _sut = new();

    private static ExecutionContext CreateContext() =>
        new(Guid.NewGuid(), Guid.NewGuid(), NullCredentialProvider.Instance);

    [Fact]
    public void WhenCreatedThenHasCorrectMetadata()
    {
        _sut.Type.Should().Be("schedule-trigger");
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
    public async Task WhenTriggerTypeIsNotScheduleThenShouldNotTrigger()
    {
        var context = new TriggerContext
        {
            TriggerData = JsonSerializer.SerializeToElement(new { triggerType = "webhook" })
        };

        var result = await _sut.ShouldTriggerAsync(context);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task WhenTriggerTypeIsScheduleWithNoTimeThenShouldTrigger()
    {
        var context = new TriggerContext
        {
            TriggerData = JsonSerializer.SerializeToElement(new { triggerType = "schedule" })
        };

        var result = await _sut.ShouldTriggerAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task WhenScheduledTimeIsWithinToleranceThenShouldTrigger()
    {
        var context = new TriggerContext
        {
            TriggerData = JsonSerializer.SerializeToElement(new
            {
                triggerType = "schedule",
                scheduledTime = DateTime.UtcNow
            })
        };

        var result = await _sut.ShouldTriggerAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task WhenScheduledTimeIsOutsideToleranceThenShouldNotTrigger()
    {
        var context = new TriggerContext
        {
            TriggerData = JsonSerializer.SerializeToElement(new
            {
                triggerType = "schedule",
                scheduledTime = DateTime.UtcNow.AddMinutes(-5)
            })
        };

        var result = await _sut.ShouldTriggerAsync(context);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task WhenExecutedWithCronExpressionThenOutputsScheduleData()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            cronExpression = "0 * * * *",
            timezone = "America/New_York"
        });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("cronExpression").GetString().Should().Be("0 * * * *");
        result.Data.GetProperty("timezone").GetString().Should().Be("America/New_York");
        result.Data.TryGetProperty("triggeredAt", out _).Should().BeTrue();
        result.Data.GetProperty("executionId").GetString().Should().Be(context.ExecutionId.ToString());
    }

    [Fact]
    public async Task WhenExecutedWithIntervalThenOutputsIntervalData()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            interval = 60
        });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("interval").GetInt32().Should().Be(60);
        result.Data.GetProperty("timezone").GetString().Should().Be("UTC");
    }

    [Fact]
    public async Task WhenExecutedWithNoConfigThenOutputsDefaultTimezone()
    {
        var input = new NodeInput
        {
            Data = default,
            Configuration = default
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("timezone").GetString().Should().Be("UTC");
    }

    [Fact]
    public void WhenGetNextExecutionTimeCalledWithValidCronThenReturnsNextTime()
    {
        var fromTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var nextTime = ScheduleTriggerNode.GetNextExecutionTime("0 * * * *", fromTime);

        nextTime.Should().NotBeNull();
        nextTime!.Value.Should().BeAfter(fromTime);
    }

    [Fact]
    public void WhenGetNextExecutionTimeCalledWithInvalidCronThenReturnsNull()
    {
        var fromTime = DateTime.UtcNow;

        var nextTime = ScheduleTriggerNode.GetNextExecutionTime("invalid", fromTime);

        nextTime.Should().BeNull();
    }
}
