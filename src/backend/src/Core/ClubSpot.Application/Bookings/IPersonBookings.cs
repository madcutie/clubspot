using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Primitives;

namespace ClubSpot.Application.Bookings;

// What bookings knows about a person, published as a contract so core can show it without
// knowing this module exists. A tenant that did not contract bookings never asks (ADR-0012):
// the person simply has no bookings, which is not an error.
public interface IPersonBookings
{
    Task<IReadOnlyDictionary<Guid, PersonBookingSummary>> SummariesAsync(
        IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken);

    // Bounded by whoever ever booked, not by the whole census: that is the smaller set, and the
    // "without bookings" filter has to run in SQL for pagination to keep meaning anything.
    Task<IReadOnlyCollection<Guid>> PeopleWithBookingsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PersonBookingItem>> HistoryAsync(Guid personId, int take, CancellationToken cancellationToken);
}

public sealed record PersonBookingSummary(int Count, DateOnly? LastOn);

public sealed record PersonBookingItem(Guid Id, DateOnly Date, int StartMinute, int DurationMinutes,
    string CourtName, Sport Sport, Money Price, decimal Paid, BookingStatus Status);
