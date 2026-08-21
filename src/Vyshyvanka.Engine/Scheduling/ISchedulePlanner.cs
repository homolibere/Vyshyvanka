namespace Vyshyvanka.Engine.Scheduling;

/// <summary>
/// Computes the next fire time for a schedule trigger from a cron expression or a fixed interval.
/// </summary>
public interface ISchedulePlanner
{
    /// <summary>
    /// Returns the next occurrence (UTC) strictly after <paramref name="fromUtc"/>, or <c>null</c>
    /// when no schedule is configured or the cron expression is invalid.
    /// </summary>
    /// <param name="cronExpression">Standard 5-field cron expression, or null when using an interval.</param>
    /// <param name="intervalSeconds">Fixed interval in seconds, or null when using a cron expression. Takes precedence when both are set.</param>
    /// <param name="fromUtc">The reference time (UTC) to compute the next occurrence after.</param>
    /// <param name="timeZoneId">IANA/Windows time zone id for cron evaluation. Defaults to UTC.</param>
    DateTime? GetNextOccurrence(
        string? cronExpression,
        int? intervalSeconds,
        DateTime fromUtc,
        string timeZoneId = "UTC");
}
