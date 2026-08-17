using ClubSpot.Domain.Bookings;

namespace ClubSpot.Application.Bookings;

public sealed record AvailabilityData(
    IReadOnlyList<Court> Courts,
    IReadOnlyDictionary<Guid, Schedule> Schedules,
    IReadOnlyList<AvailabilityOverride> Overrides,
    IReadOnlyList<Booking> ConfirmedBookings);

public interface IAvailabilityQueries
{
    Task<AvailabilityData> GetDataAsync(Sport sport, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
