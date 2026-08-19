using ClubSpot.Application.Core;
using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Time;

namespace ClubSpot.Application.Bookings;

public sealed record PortalClub(string Name, string? Venue, string Currency, int DepositPercent);

public sealed record PortalCourt(Guid Id, string Name, string Detail, bool IsCovered, int[] Durations);

public sealed record PortalSport(Sport Sport, IReadOnlyList<PortalCourt> Courts);

public sealed record PortalCatalog(PortalClub Club, IReadOnlyList<PortalSport> Sports);

public sealed record PortalSlot(int StartMinute, int Duration, decimal Price);

public sealed record PortalDayCourt(Guid CourtId, IReadOnlyList<PortalSlot> Slots);

public sealed record PortalDay(DateOnly Date, IReadOnlyList<PortalDayCourt> Courts);

public sealed record PortalAvailability(string Currency, IReadOnlyList<PortalDay> Days);

public enum PortalAvailabilityOutcome { Ok, RangeTooLong }

public sealed record PortalAvailabilityResult(PortalAvailabilityOutcome Outcome, PortalAvailability? Availability);

public sealed class GetPortalCatalogHandler(IClubSettings clubSettings, ICourtsStore courtsStore)
{
    public async Task<PortalCatalog> HandleAsync(CancellationToken cancellationToken)
    {
        var club = await clubSettings.GetAsync(cancellationToken);
        var snapshots = await courtsStore.GetAllAsync(cancellationToken);
        var sports = snapshots
            .Where(snapshot => snapshot.Court.IsActive)
            .GroupBy(snapshot => snapshot.Court.Sport)
            .Select(group => new PortalSport(group.Key, group
                .Select(snapshot => new PortalCourt(snapshot.Court.Id, snapshot.Court.Name, snapshot.Court.Detail,
                    snapshot.Court.IsCovered, snapshot.Court.Durations))
                .ToList()))
            .ToList();
        return new PortalCatalog(new PortalClub(club.Name, club.Venue, club.Currency, club.DepositPercent), sports);
    }
}

public sealed class GetPortalAvailabilityHandler(IAvailabilityQueries queries, IClubSettings clubSettings, IClock clock)
{
    private const int MaxRangeDays = 31;

    public async Task<PortalAvailabilityResult> HandleAsync(Sport sport, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var club = await clubSettings.GetAsync(cancellationToken);
        var calendar = new ClubCalendar(TimeZoneInfo.FindSystemTimeZoneById(club.TimeZone), clock);
        var today = calendar.Today();
        var now = calendar.Now();
        var nowMinute = now.Hour * 60 + now.Minute;

        if (from < today) from = today;
        if (to < from)
            return new PortalAvailabilityResult(PortalAvailabilityOutcome.Ok, new PortalAvailability(club.Currency, []));
        if (to.DayNumber - from.DayNumber + 1 > MaxRangeDays)
            return new PortalAvailabilityResult(PortalAvailabilityOutcome.RangeTooLong, null);

        var data = await queries.GetDataAsync(sport, from, to, cancellationToken);
        var days = new List<PortalDay>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var currentDate = date;
            // A court pointing at a missing schedule offers nothing; it does not take the whole
            // sport's availability down with it.
            var courts = data.Courts
                .Where(court => data.Schedules.ContainsKey(court.ScheduleId))
                .Select(court => new PortalDayCourt(court.Id, AvailabilityCalculator
                    .SlotsFor(court, data.Schedules[court.ScheduleId], data.Overrides,
                        data.ActiveBookings.Where(booking => booking.CourtId == court.Id && booking.Date == currentDate).ToList(),
                        currentDate, today, nowMinute)
                    .Select(slot => new PortalSlot(slot.StartMinute, slot.DurationMinutes, slot.Price.Amount))
                    .ToList()))
                .ToList();
            days.Add(new PortalDay(date, courts));
        }
        return new PortalAvailabilityResult(PortalAvailabilityOutcome.Ok, new PortalAvailability(club.Currency, days));
    }
}
