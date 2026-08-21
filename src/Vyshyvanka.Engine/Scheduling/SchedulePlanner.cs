using Cronos;

namespace Vyshyvanka.Engine.Scheduling;

/// <summary>
/// <see cref="ISchedulePlanner"/> implementation backed by <see cref="CronExpression"/> (Cronos)
/// for cron parsing, with a simple additive rule for fixed intervals.
/// </summary>
public sealed class SchedulePlanner : ISchedulePlanner
{
    /// <inheritdoc />
    public DateTime? GetNextOccurrence(
        string? cronExpression,
        int? intervalSeconds,
        DateTime fromUtc,
        string timeZoneId = "UTC")
    {
        // Interval takes precedence when both are configured.
        if (intervalSeconds is > 0)
        {
            return DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc).AddSeconds(intervalSeconds.Value);
        }

        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            return null;
        }

        CronExpression expression;
        try
        {
            expression = CronExpression.Parse(cronExpression, CronFormat.Standard);
        }
        catch (CronFormatException)
        {
            return null;
        }

        var timeZone = ResolveTimeZone(timeZoneId);
        var fromUtcNormalized = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc);

        return expression.GetNextOccurrence(fromUtcNormalized, timeZone, inclusive: false);
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
