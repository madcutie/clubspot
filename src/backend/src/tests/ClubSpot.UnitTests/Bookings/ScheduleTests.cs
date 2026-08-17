using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.UnitTests.Bookings;

public sealed class ScheduleTests
{
    [Fact]
    public void Overlapping_time_ranges_are_rejected()
    {
        var ranges = new Dictionary<DayOfWeek, List<TimeRange>>
        {
            [DayOfWeek.Monday] = [new(480, 720), new(600, 900)]
        };

        Assert.Throws<ArgumentException>(() => new Schedule(
            Guid.NewGuid(), TenantId.From(Guid.NewGuid()), "Weekdays", ranges));
    }

    [Fact]
    public void Identical_ranges_on_different_days_are_valid()
    {
        var ranges = new Dictionary<DayOfWeek, List<TimeRange>>
        {
            [DayOfWeek.Monday] = [new(480, 1380)],
            [DayOfWeek.Tuesday] = [new(480, 1380)]
        };

        var schedule = new Schedule(Guid.NewGuid(), TenantId.From(Guid.NewGuid()), "Base", ranges);

        Assert.Equal(2, schedule.WeeklyRanges.Count);
    }
}
