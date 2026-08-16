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
            Guid.NewGuid(), TenantId.From(Guid.NewGuid()), "Weekdays", "America/Argentina/Buenos_Aires", ranges, []));
    }
}
