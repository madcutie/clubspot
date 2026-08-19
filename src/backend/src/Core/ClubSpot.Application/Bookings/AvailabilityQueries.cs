using ClubSpot.Domain.Bookings;

namespace ClubSpot.Application.Bookings;

public sealed record AvailabilityData(
    IReadOnlyList<Court> Courts,
    IReadOnlyDictionary<Guid, Schedule> Schedules,
    IReadOnlyList<AvailabilityOverride> Overrides,
    IReadOnlyList<Booking> ActiveBookings);

public sealed record InactiveBooking(Booking Booking, decimal PaidAmount);

public interface IAvailabilityQueries
{
    Task<AvailabilityData> GetDataAsync(Sport sport, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    // Cancelled and dead-hold bookings of the day: they don't block, but the agenda shows them.
    Task<IReadOnlyList<InactiveBooking>> GetInactiveBookingsAsync(
        IReadOnlyCollection<Guid> courtIds, DateOnly date, CancellationToken cancellationToken);
}
