using ClubSpot.Application.Bookings;
using ClubSpot.Domain.Bookings;
using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace ClubSpot.Infrastructure.Repositories;

internal sealed class AvailabilityQueries(ClubSpotDbContext db, IClock clock) : IAvailabilityQueries
{
    public async Task<AvailabilityData> GetDataAsync(Sport sport, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var courts = await db.Courts.AsNoTracking()
            .Where(court => court.Sport == sport && court.IsActive)
            .OrderBy(court => court.SortOrder)
            .ToListAsync(cancellationToken);

        var scheduleIds = courts.Select(court => court.ScheduleId).Distinct().ToList();
        var schedules = await db.Schedules.AsNoTracking()
            .Where(schedule => scheduleIds.Contains(schedule.Id))
            .ToDictionaryAsync(schedule => schedule.Id, cancellationToken);

        var courtIds = courts.Select(court => court.Id).ToList();
        var overrides = await db.AvailabilityOverrides.AsNoTracking()
            .Where(availabilityOverride => availabilityOverride.CourtId == null
                || courtIds.Contains(availabilityOverride.CourtId.Value))
            .Where(availabilityOverride => availabilityOverride.Dates.Any(date => date.Date >= from && date.Date <= to))
            .Include(availabilityOverride => availabilityOverride.Dates)
            .ToListAsync(cancellationToken);

        // A live hold blocks the slot exactly like a confirmed booking; expired ones don't.
        var now = clock.UtcNow;
        var activeBookings = await db.Bookings.AsNoTracking()
            .Where(booking => courtIds.Contains(booking.CourtId)
                && booking.Date >= from && booking.Date <= to
                && (booking.Status == BookingStatus.Confirmed
                    || (booking.Status == BookingStatus.PendingPayment && booking.ExpiresAt > now)))
            .ToListAsync(cancellationToken);

        return new AvailabilityData(courts, schedules, overrides, activeBookings);
    }

    public async Task<IReadOnlyList<InactiveBooking>> GetInactiveBookingsAsync(
        IReadOnlyCollection<Guid> courtIds, DateOnly date, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var bookings = await db.Bookings.AsNoTracking()
            .Where(booking => courtIds.Contains(booking.CourtId) && booking.Date == date
                && (booking.Status == BookingStatus.Cancelled || booking.Status == BookingStatus.Expired
                    || (booking.Status == BookingStatus.PendingPayment && booking.ExpiresAt <= now)))
            .OrderBy(booking => booking.StartMinute)
            .ToListAsync(cancellationToken);

        var bookingIds = bookings.Select(booking => booking.Id).ToList();
        // Orphans count as paid: money on a dead booking is exactly what the operator must see.
        var paidByBooking = await db.Payments.AsNoTracking()
            .Where(payment => bookingIds.Contains(payment.BookingId)
                && (payment.Status == PaymentStatus.Approved || payment.Status == PaymentStatus.ApprovedOrphan))
            .GroupBy(payment => payment.BookingId)
            .Select(group => new { group.Key, Total = group.Sum(payment => payment.Amount.Amount) })
            .ToDictionaryAsync(entry => entry.Key, entry => entry.Total, cancellationToken);

        return bookings
            .Select(booking => new InactiveBooking(booking, paidByBooking.GetValueOrDefault(booking.Id)))
            .ToList();
    }
}
