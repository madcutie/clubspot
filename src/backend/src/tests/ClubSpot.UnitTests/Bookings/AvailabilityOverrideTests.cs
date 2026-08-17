using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.UnitTests.Bookings;

public sealed class AvailabilityOverrideTests
{
    [Fact]
    public void An_availability_override_requires_at_least_one_date()
    {
        Assert.Throws<ArgumentException>(() => new AvailabilityOverride(
            Guid.NewGuid(), TenantId.From(Guid.NewGuid()), null, [], [], null,
            DateTimeOffset.UtcNow, Guid.NewGuid()));
    }

    [Fact]
    public void Duplicate_dates_are_rejected()
    {
        var date = new DateOnly(2026, 8, 20);

        Assert.Throws<ArgumentException>(() => new AvailabilityOverride(
            Guid.NewGuid(), TenantId.From(Guid.NewGuid()), null, [date, date], [], null,
            DateTimeOffset.UtcNow, Guid.NewGuid()));
    }

    [Fact]
    public void Overlapping_windows_are_rejected()
    {
        var windows = new List<TimeRange> { new(480, 720), new(600, 900) };

        Assert.Throws<ArgumentException>(() => new AvailabilityOverride(
            Guid.NewGuid(), TenantId.From(Guid.NewGuid()), null, [new DateOnly(2026, 8, 20)], windows, null,
            DateTimeOffset.UtcNow, Guid.NewGuid()));
    }

    [Fact]
    public void Empty_windows_mean_closed_and_are_accepted()
    {
        var availabilityOverride = new AvailabilityOverride(
            Guid.NewGuid(), TenantId.From(Guid.NewGuid()), null, [new DateOnly(2026, 8, 20)], [], null,
            DateTimeOffset.UtcNow, Guid.NewGuid());

        Assert.Empty(availabilityOverride.Windows);
    }

    [Fact]
    public void Reason_longer_than_200_characters_is_rejected()
    {
        var reason = new string('a', 201);

        Assert.Throws<ArgumentOutOfRangeException>(() => new AvailabilityOverride(
            Guid.NewGuid(), TenantId.From(Guid.NewGuid()), null, [new DateOnly(2026, 8, 20)], [], reason,
            DateTimeOffset.UtcNow, Guid.NewGuid()));
    }

    [Fact]
    public void Dates_are_stored_in_ascending_order()
    {
        var dates = new List<DateOnly> { new(2026, 8, 22), new(2026, 8, 20), new(2026, 8, 21) };

        var availabilityOverride = new AvailabilityOverride(
            Guid.NewGuid(), TenantId.From(Guid.NewGuid()), null, dates, [], null,
            DateTimeOffset.UtcNow, Guid.NewGuid());

        Assert.Equal(
            [new DateOnly(2026, 8, 20), new DateOnly(2026, 8, 21), new DateOnly(2026, 8, 22)],
            availabilityOverride.Dates.Select(date => date.Date));
    }
}
