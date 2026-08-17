using ClubSpot.Application.Core;
using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Time;

namespace ClubSpot.Application.Bookings;

public sealed record AgendaSlot(int StartMinute, int Duration, decimal Price);

public sealed record AgendaBooking(Guid Id, int StartMinute, int DurationMinutes, string CustomerName,
    string? CustomerPhone, decimal Price, BookingStatus Status);

public sealed record AgendaCourt(Guid CourtId, string Name, string Detail, bool IsCovered,
    IReadOnlyList<TimeRange> Windows, IReadOnlyList<AgendaSlot> Slots, IReadOnlyList<AgendaBooking> Bookings);

public sealed record Agenda(string Currency, IReadOnlyList<AgendaCourt> Courts);

public sealed class GetAgendaHandler(IAvailabilityQueries queries, IClubSettings clubSettings, IClock clock)
{
    public async Task<Agenda> HandleAsync(Sport sport, DateOnly date, CancellationToken cancellationToken)
    {
        var club = await clubSettings.GetAsync(cancellationToken);
        var calendar = new ClubCalendar(TimeZoneInfo.FindSystemTimeZoneById(club.TimeZone), clock);
        var today = calendar.Today();

        var data = await queries.GetDataAsync(sport, date, date, cancellationToken);
        var courts = data.Courts.Select(court =>
        {
            var schedule = data.Schedules[court.ScheduleId];
            var bookings = data.ActiveBookings
                .Where(booking => booking.CourtId == court.Id && booking.Date == date)
                .OrderBy(booking => booking.StartMinute)
                .ToList();
            // The operator sells on the spot: a negative now-minute keeps the minimum notice from cutting starts.
            var slots = AvailabilityCalculator
                .SlotsFor(court, schedule, data.Overrides, bookings, date, today, -court.MinimumNoticeMinutes)
                .Select(slot => new AgendaSlot(slot.StartMinute, slot.DurationMinutes, slot.Price.Amount))
                .ToList();
            return new AgendaCourt(court.Id, court.Name, court.Detail, court.IsCovered,
                AvailabilityCalculator.EffectiveWindows(court, schedule, data.Overrides, date),
                slots,
                bookings.Select(booking => new AgendaBooking(booking.Id, booking.StartMinute, booking.DurationMinutes,
                    booking.CustomerName, booking.CustomerPhone, booking.Price.Amount, booking.Status)).ToList());
        }).ToList();
        return new Agenda(club.Currency, courts);
    }
}
