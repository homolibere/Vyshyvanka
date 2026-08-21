using AwesomeAssertions;
using CsCheck;
using Vyshyvanka.Engine.Scheduling;

namespace Vyshyvanka.Tests.Unit.Scheduling;

public class SchedulePlannerTests
{
    private readonly SchedulePlanner _sut = new();

    [Fact]
    public void WhenCronIsWeekdaysAt9ThenNextOccurrenceMatches()
    {
        // Monday 2026-01-05 08:00 UTC -> next weekday-9am is same day 09:00.
        var from = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc);

        var next = _sut.GetNextOccurrence("0 9 * * 1-5", null, from);

        next.Should().Be(new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void WhenCronIsEvery15MinutesThenNextOccurrenceIsNextQuarter()
    {
        var from = new DateTime(2026, 1, 5, 8, 7, 0, DateTimeKind.Utc);

        var next = _sut.GetNextOccurrence("*/15 * * * *", null, from);

        next.Should().Be(new DateTime(2026, 1, 5, 8, 15, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void WhenIntervalIsSetThenNextOccurrenceIsFromPlusInterval()
    {
        var from = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc);

        var next = _sut.GetNextOccurrence(null, 300, from);

        next.Should().Be(from.AddSeconds(300));
    }

    [Fact]
    public void WhenBothCronAndIntervalSetThenIntervalWins()
    {
        var from = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc);

        var next = _sut.GetNextOccurrence("0 9 * * *", 60, from);

        next.Should().Be(from.AddSeconds(60));
    }

    [Fact]
    public void WhenCronIsInvalidThenReturnsNull()
    {
        var from = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc);

        var next = _sut.GetNextOccurrence("not a cron", null, from);

        next.Should().BeNull();
    }

    [Fact]
    public void WhenNeitherCronNorIntervalThenReturnsNull()
    {
        var from = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc);

        var next = _sut.GetNextOccurrence(null, null, from);

        next.Should().BeNull();
    }

    [Fact]
    public void WhenUnknownTimezoneThenFallsBackToUtc()
    {
        var from = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc);

        var next = _sut.GetNextOccurrence("0 9 * * *", null, from, "Not/AZone");

        next.Should().Be(new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void WhenCronValidThenNextOccurrenceIsAlwaysStrictlyAfterFrom()
    {
        // Property: for a fixed valid cron, the next occurrence is always in the future.
        Gen.DateTime[new DateTime(2000, 1, 1), new DateTime(2100, 1, 1)]
            .Sample(from =>
            {
                var fromUtc = DateTime.SpecifyKind(from, DateTimeKind.Utc);
                var next = _sut.GetNextOccurrence("0 * * * *", null, fromUtc);

                next.Should().NotBeNull();
                next!.Value.Should().BeAfter(fromUtc);
            });
    }
}
