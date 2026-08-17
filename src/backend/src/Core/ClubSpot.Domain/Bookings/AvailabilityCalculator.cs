using ClubSpot.SharedKernel.Primitives;

namespace ClubSpot.Domain.Bookings;

public static class AvailabilityCalculator
{
    public static IReadOnlyList<AvailableSlot> SlotsFor(
        Court court,
        Schedule schedule,
        IReadOnlyList<AvailabilityOverride> overrides,
        IReadOnlyList<Booking> bookings,
        DateOnly date,
        DateOnly todayAtClub,
        int nowMinuteAtClub)
    {
        if (!court.IsActive || date < todayAtClub) return [];

        var windows = EffectiveWindows(court, schedule, overrides, date);
        var noticeFloor = (date.DayNumber - todayAtClub.DayNumber) * 1440 - nowMinuteAtClub;

        var slots = new List<AvailableSlot>();
        foreach (var duration in court.Durations)
        {
            foreach (var window in windows)
            {
                var start = CeilToIncrement(window.OpensAtMinute, court.StartIncrementMinutes);
                for (; start + duration <= window.ClosesAtMinute; start += court.StartIncrementMinutes)
                {
                    if (noticeFloor + start < court.MinimumNoticeMinutes) continue;
                    if (OverlapsConfirmedBooking(bookings, start, duration)) continue;
                    slots.Add(new AvailableSlot(start, duration, PriceFor(court, start, duration)));
                }
            }
        }

        return slots
            .OrderBy(slot => slot.StartMinute)
            .ThenBy(slot => slot.DurationMinutes)
            .ToList();
    }

    public static IReadOnlyList<TimeRange> EffectiveWindows(
        Court court, Schedule schedule, IReadOnlyList<AvailabilityOverride> overrides, DateOnly date)
    {
        var courtOverride = MostRecentOverride(overrides, date, o => o.CourtId == court.Id);
        if (courtOverride is not null) return courtOverride.Windows;

        var clubOverride = MostRecentOverride(overrides, date, o => o.CourtId is null);
        if (clubOverride is not null) return clubOverride.Windows;

        return schedule.WeeklyRanges.TryGetValue(date.DayOfWeek, out var ranges) ? ranges : [];
    }

    private static int CeilToIncrement(int minute, int increment) =>
        (minute + increment - 1) / increment * increment;

    private static bool OverlapsConfirmedBooking(IReadOnlyList<Booking> bookings, int start, int duration) =>
        bookings.Any(booking => booking.Status == BookingStatus.Confirmed
            && booking.StartMinute < start + duration
            && start < booking.StartMinute + booking.DurationMinutes);

    private static AvailabilityOverride? MostRecentOverride(
        IReadOnlyList<AvailabilityOverride> overrides, DateOnly date, Func<AvailabilityOverride, bool> scope) =>
        overrides
            .Where(scope)
            .Where(availabilityOverride => availabilityOverride.Dates.Any(d => d.Date == date))
            .OrderByDescending(availabilityOverride => availabilityOverride.CreatedAt)
            .FirstOrDefault();

    private static Money PriceFor(Court court, int start, int durationMinutes)
    {
        var hourlyRate = start < court.NightStartsAtMinute ? court.DayPrice : court.NightPrice;
        // Provisional linear scaling; replaced by the tariffs ADR (per-duration prices).
        return hourlyRate * (durationMinutes / 60m);
    }
}
