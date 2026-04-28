using System.Text.Json;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Nodes.Base;
using Vyshyvanka.Core.Attributes;

namespace Vyshyvanka.Engine.Nodes.Triggers;

/// <summary>
/// A trigger node that activates based on a schedule (cron expression or interval).
/// </summary>
[NodeDefinition(
    Name = "Schedule Trigger",
    Description = "Trigger workflow on a schedule using cron expressions or intervals",
    Icon = "fa-solid fa-clock")]
[NodeOutput("output", DisplayName = "Schedule Data")]
[ConfigurationProperty("cronExpression", "string", Description = "Cron expression for scheduling")]
[ConfigurationProperty("interval", "number", Description = "Interval in seconds (alternative to cron)")]
[ConfigurationProperty("timezone", "string", Description = "Timezone for schedule evaluation")]
public class ScheduleTriggerNode : BaseTriggerNode
{
    private string _id = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public override string Id => _id;

    /// <inheritdoc />
    public override string Type => "schedule-trigger";

    /// <inheritdoc />
    public override Task<bool> ShouldTriggerAsync(TriggerContext context)
    {
        // Check if the trigger context indicates a scheduled execution
        if (context.TriggerData.ValueKind == JsonValueKind.Undefined ||
            context.TriggerData.ValueKind == JsonValueKind.Null)
        {
            return Task.FromResult(false);
        }

        var triggerType = GetTriggerValue<string>(context, "triggerType");
        if (triggerType != "schedule")
        {
            return Task.FromResult(false);
        }

        // Verify the scheduled time matches (within tolerance)
        var scheduledTime = GetTriggerValue<DateTime?>(context, "scheduledTime");
        if (scheduledTime.HasValue)
        {
            var tolerance = TimeSpan.FromSeconds(60); // 1 minute tolerance
            var now = DateTime.UtcNow;
            return Task.FromResult(Math.Abs((now - scheduledTime.Value).TotalSeconds) <= tolerance.TotalSeconds);
        }

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public override Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        var cronExpression = GetConfigValue<string>(input, "cronExpression");
        var interval = GetConfigValue<int?>(input, "interval");
        var timezone = GetConfigValue<string>(input, "timezone") ?? "UTC";

        var scheduleData = new Dictionary<string, object?>
        {
            ["triggeredAt"] = DateTime.UtcNow,
            ["cronExpression"] = cronExpression,
            ["interval"] = interval,
            ["timezone"] = timezone,
            ["executionId"] = context.ExecutionId
        };

        return Task.FromResult(SuccessOutput(scheduleData));
    }

    /// <summary>
    /// Calculates the next execution time based on the cron expression.
    /// </summary>
    public static DateTime? GetNextExecutionTime(string cronExpression, DateTime fromTime, string timezone = "UTC")
    {
        // Basic cron parsing - in production, use a library like Cronos
        // Format: minute hour day month dayOfWeek
        var parts = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5)
            return null;

        // For now, return a simple next minute calculation
        // A full implementation would parse the cron expression properly
        return fromTime.AddMinutes(1);
    }
}
