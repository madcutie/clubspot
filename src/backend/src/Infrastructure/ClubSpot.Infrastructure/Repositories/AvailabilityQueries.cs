using ClubSpot.Application.Bookings;
using ClubSpot.Domain.Bookings;
using ClubSpot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClubSpot.Infrastructure.Repositories;

internal sealed class AvailabilityQueries(ClubSpotDbContext db) : IAvailabilityQueries
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

        var confirmedBookings = await db.Bookings.AsNoTracking()
            .Where(booking => courtIds.Contains(booking.CourtId)
                && booking.Date >= from && booking.Date <= to
                && booking.Status == BookingStatus.Confirmed)
            .ToListAsync(cancellationToken);

        return new AvailabilityData(courts, schedules, overrides, confirmedBookings);
    }
}
