using ClubSpot.Application.Bookings;
using ClubSpot.Application.Core;
using ClubSpot.Domain.Bookings;
using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Tenancy;
using ClubSpot.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClubSpot.Infrastructure.Repositories;

internal sealed class BookingsStore(
    ClubSpotDbContext db, ITenantContext tenantContext, IClubSettings clubSettings, IClock clock) : IBookingsStore
{
    public async Task<BookingCreateResult> CreateAsync(BookingCreateInput input, CancellationToken cancellationToken)
    {
        var court = await db.Courts.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == input.CourtId, cancellationToken);
        if (court is null || !court.IsActive)
            return new BookingCreateResult(BookingCreateOutcome.UnknownCourt, Guid.Empty, default);

        var schedule = await db.Schedules.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == court.ScheduleId, cancellationToken);
        var overrides = await db.AvailabilityOverrides.AsNoTracking()
            .Where(availabilityOverride => availabilityOverride.CourtId == null || availabilityOverride.CourtId == court.Id)
            .Where(availabilityOverride => availabilityOverride.Dates.Any(date => date.Date == input.Date))
            .Include(availabilityOverride => availabilityOverride.Dates)
            .ToListAsync(cancellationToken);

        var club = await clubSettings.GetAsync(cancellationToken);
        var calendar = new ClubCalendar(TimeZoneInfo.FindSystemTimeZoneById(club.TimeZone), clock);
        // The operator sells on the spot: a negative now-minute keeps the minimum notice from cutting starts.
        var slots = AvailabilityCalculator.SlotsFor(
            court, schedule, overrides, [], input.Date, calendar.Today(), -court.MinimumNoticeMinutes);
        var slot = slots.FirstOrDefault(candidate =>
            candidate.StartMinute == input.StartMinute && candidate.DurationMinutes == input.DurationMinutes);
        if (slot is null) return new BookingCreateResult(BookingCreateOutcome.InvalidSlot, Guid.Empty, default);

        var taken = await db.Bookings.AnyAsync(booking => booking.CourtId == court.Id && booking.Date == input.Date
            && booking.Status == BookingStatus.Confirmed
            && booking.StartMinute < input.StartMinute + input.DurationMinutes
            && input.StartMinute < booking.StartMinute + booking.DurationMinutes, cancellationToken);
        if (taken) return new BookingCreateResult(BookingCreateOutcome.SlotTaken, Guid.Empty, default);

        var booking = new Booking(Guid.NewGuid(), tenantContext.Current, court.Id, input.Date, input.StartMinute,
            input.DurationMinutes, slot.Price, input.CustomerName, input.CustomerPhone, clock.UtcNow, input.CreatedBy);
        db.Bookings.Add(booking);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            return new BookingCreateResult(BookingCreateOutcome.SlotTaken, Guid.Empty, default);
        }
        return new BookingCreateResult(BookingCreateOutcome.Created, booking.Id, booking.Price);
    }

    public async Task<BookingCancelOutcome> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (booking is null) return BookingCancelOutcome.NotFound;

        if (booking.Status != BookingStatus.Cancelled)
        {
            booking.Cancel(clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
        }
        return BookingCancelOutcome.Cancelled;
    }
}
