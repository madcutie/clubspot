using ClubSpot.Application.Bookings;
using ClubSpot.Application.Core;
using ClubSpot.Domain.Bookings;
using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Primitives;
using ClubSpot.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace ClubSpot.Infrastructure.Repositories;

internal sealed class PersonBookings(ClubSpotDbContext db, IClubSettings clubSettings, IClock clock) : IPersonBookings
{
    // A cancelled or expired slot was never played: it counts for nothing on the person's card.
    private static readonly BookingStatus[] Counted = [BookingStatus.Confirmed, BookingStatus.PendingPayment];

    public async Task<IReadOnlyDictionary<Guid, PersonBookingSummary>> SummariesAsync(
        IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken)
    {
        if (personIds.Count == 0) return new Dictionary<Guid, PersonBookingSummary>();

        var today = await TodayAsync(cancellationToken);
        var rows = await db.Bookings.AsNoTracking()
            .Where(booking => booking.PersonId != null && personIds.Contains(booking.PersonId.Value)
                && Counted.Contains(booking.Status))
            .GroupBy(booking => booking.PersonId!.Value)
            .Select(group => new
            {
                PersonId = group.Key,
                Count = group.Count(),
                // "Last time" is the last time they played, so a slot booked for next week is not it.
                LastOn = group.Max(booking => booking.Date <= today ? (DateOnly?)booking.Date : null)
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.PersonId, row => new PersonBookingSummary(row.Count, row.LastOn));
    }

    public async Task<IReadOnlyCollection<Guid>> PeopleWithBookingsAsync(CancellationToken cancellationToken) =>
        await db.Bookings.AsNoTracking()
            .Where(booking => booking.PersonId != null && Counted.Contains(booking.Status))
            .Select(booking => booking.PersonId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PersonBookingItem>> HistoryAsync(Guid personId, int take,
        CancellationToken cancellationToken)
    {
        var bookings = await (from booking in db.Bookings.AsNoTracking()
                              join court in db.Courts.AsNoTracking() on booking.CourtId equals court.Id
                              where booking.PersonId == personId && Counted.Contains(booking.Status)
                              orderby booking.Date descending, booking.StartMinute descending
                              select new { booking, court.Name, court.Sport })
            .Take(take)
            .ToListAsync(cancellationToken);

        var ids = bookings.Select(row => row.booking.Id).ToList();
        // Same definition of "paid" as everywhere else: orphan money is not money this slot has.
        var paid = await db.Payments.AsNoTracking()
            .Where(payment => ids.Contains(payment.BookingId) && payment.Status == PaymentStatus.Approved)
            .GroupBy(payment => payment.BookingId)
            .Select(group => new { group.Key, Total = group.Sum(payment => payment.Amount.Amount) })
            .ToDictionaryAsync(entry => entry.Key, entry => entry.Total, cancellationToken);

        return bookings.Select(row => new PersonBookingItem(row.booking.Id, row.booking.Date,
            row.booking.StartMinute, row.booking.DurationMinutes, row.Name, row.Sport, row.booking.Price,
            paid.GetValueOrDefault(row.booking.Id), row.booking.Status)).ToList();
    }

    private async Task<DateOnly> TodayAsync(CancellationToken cancellationToken)
    {
        var club = await clubSettings.GetAsync(cancellationToken);
        return new ClubCalendar(TimeZoneInfo.FindSystemTimeZoneById(club.TimeZone), clock).Today();
    }
}
