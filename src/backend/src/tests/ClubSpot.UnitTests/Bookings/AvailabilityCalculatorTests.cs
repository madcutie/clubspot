using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Primitives;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.UnitTests.Bookings;

public sealed class AvailabilityCalculatorTests
{
    private static readonly TenantId Tenant = TenantId.From(Guid.NewGuid());
    private static readonly Guid CourtId = Guid.NewGuid();
    private static readonly Guid OtherCourtId = Guid.NewGuid();
    private static readonly Guid ScheduleId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 8, 17);
    private static readonly DateOnly Tomorrow = Today.AddDays(1);

    private static Court MakeCourt(
        Guid? id = null,
        bool isActive = true,
        int[]? durations = null,
        int startIncrementMinutes = 30,
        int minimumNoticeMinutes = 0,
        decimal dayPrice = 100m,
        decimal nightPrice = 200m,
        int nightStartsAtMinute = 1200) =>
        new(id ?? CourtId, Tenant, Sport.Padel, 1, "Court 1", "", false, isActive, ScheduleId,
            durations ?? [60], startIncrementMinutes, minimumNoticeMinutes,
            Money.Of(dayPrice, "ARS"), Money.Of(nightPrice, "ARS"), nightStartsAtMinute);

    private static Schedule MakeSchedule(Dictionary<DayOfWeek, List<TimeRange>>? ranges = null) =>
        new(ScheduleId, Tenant, "Schedule", ranges ?? new Dictionary<DayOfWeek, List<TimeRange>>
        {
            [Today.DayOfWeek] = [new TimeRange(480, 1380)]
        });

    private static AvailabilityOverride MakeOverride(
        Guid? courtId, DateOnly[] dates, TimeRange[] windows, DateTimeOffset createdAt) =>
        new(Guid.NewGuid(), Tenant, courtId, dates, windows, null, createdAt, Guid.NewGuid());

    private static Booking MakeBooking(int startMinute, int durationMinutes) =>
        new(Guid.NewGuid(), Tenant, CourtId, Today, startMinute, durationMinutes,
            Money.Of(14000m, "ARS"), "Ana Suarez", null, null, BookingOrigin.Counter, DateTimeOffset.UtcNow, Guid.NewGuid());

    private static Booking MakeHold(int startMinute, int durationMinutes)
    {
        var createdAt = DateTimeOffset.UtcNow;
        return Booking.Hold(Guid.NewGuid(), Tenant, CourtId, Today, startMinute, durationMinutes,
            Money.Of(14000m, "ARS"), "Ana Suarez", null, Guid.NewGuid(), BookingOrigin.Portal,
            PaymentMode.OnlineFull, createdAt.AddMinutes(5), createdAt, null);
    }

    [Fact]
    public void The_weekly_pattern_alone_generates_the_expected_starts()
    {
        var schedule = MakeSchedule(new Dictionary<DayOfWeek, List<TimeRange>>
        {
            [Today.DayOfWeek] = [new TimeRange(480, 600)]
        });
        var court = MakeCourt(durations: [60], startIncrementMinutes: 30);

        var slots = AvailabilityCalculator.SlotsFor(court, schedule, [], [], Today, Today, 0);

        Assert.Equal([480, 510, 540], slots.Select(slot => slot.StartMinute));
    }

    [Fact]
    public void A_day_with_no_ranges_in_the_pattern_is_closed()
    {
        var schedule = MakeSchedule(new Dictionary<DayOfWeek, List<TimeRange>>());
        var court = MakeCourt();

        var slots = AvailabilityCalculator.SlotsFor(court, schedule, [], [], Today, Today, 0);

        Assert.Empty(slots);
    }

    [Fact]
    public void A_closed_club_wide_override_clears_the_day()
    {
        var schedule = MakeSchedule();
        var court = MakeCourt();
        var closed = MakeOverride(null, [Today], [], DateTimeOffset.UtcNow);

        var slots = AvailabilityCalculator.SlotsFor(court, schedule, [closed], [], Today, Today, 0);

        Assert.Empty(slots);
    }

    [Fact]
    public void A_court_specific_closed_override_does_not_affect_another_court()
    {
        var schedule = MakeSchedule();
        var otherCourt = MakeCourt(id: OtherCourtId);
        var closedForCourt = MakeOverride(CourtId, [Today], [], DateTimeOffset.UtcNow);

        var slots = AvailabilityCalculator.SlotsFor(otherCourt, schedule, [closedForCourt], [], Today, Today, 0);

        Assert.NotEmpty(slots);
    }

    [Fact]
    public void Partial_windows_produce_exactly_the_expected_starts()
    {
        var schedule = MakeSchedule();
        var court = MakeCourt(durations: [60], startIncrementMinutes: 30);
        var partial = MakeOverride(
            null, [Today], [new TimeRange(480, 540), new TimeRange(660, 780)], DateTimeOffset.UtcNow);

        var slots = AvailabilityCalculator.SlotsFor(court, schedule, [partial], [], Today, Today, 0);

        Assert.Equal([480, 660, 690, 720], slots.Select(slot => slot.StartMinute));
    }

    [Fact]
    public void A_court_specific_override_wins_over_a_club_wide_override_on_the_same_day()
    {
        var schedule = MakeSchedule();
        var court = MakeCourt(durations: [60], startIncrementMinutes: 30);
        var clubClosed = MakeOverride(null, [Today], [], DateTimeOffset.UtcNow);
        var courtOpen = MakeOverride(CourtId, [Today], [new TimeRange(600, 660)], DateTimeOffset.UtcNow);

        var slots = AvailabilityCalculator.SlotsFor(court, schedule, [clubClosed, courtOpen], [], Today, Today, 0);

        Assert.Equal([600], slots.Select(slot => slot.StartMinute));
    }

    [Fact]
    public void When_two_overrides_share_scope_and_date_the_most_recently_created_wins()
    {
        var schedule = MakeSchedule();
        var court = MakeCourt(durations: [60], startIncrementMinutes: 30);
        var older = MakeOverride(CourtId, [Today], [new TimeRange(480, 600)], DateTimeOffset.UtcNow.AddMinutes(-10));
        var newer = MakeOverride(CourtId, [Today], [new TimeRange(840, 960)], DateTimeOffset.UtcNow);

        var slots = AvailabilityCalculator.SlotsFor(court, schedule, [older, newer], [], Today, Today, 0);

        Assert.Equal([840, 870, 900], slots.Select(slot => slot.StartMinute));
    }

    [Fact]
    public void An_override_only_applies_on_the_dates_in_its_set()
    {
        var schedule = MakeSchedule(new Dictionary<DayOfWeek, List<TimeRange>>
        {
            [Tomorrow.DayOfWeek] = [new TimeRange(480, 600)]
        });
        var court = MakeCourt(durations: [60], startIncrementMinutes: 30);
        var closedToday = MakeOverride(null, [Today], [], DateTimeOffset.UtcNow);

        var slotsTomorrow = AvailabilityCalculator.SlotsFor(court, schedule, [closedToday], [], Tomorrow, Today, 0);

        Assert.NotEmpty(slotsTomorrow);
    }

    [Fact]
    public void A_60_minute_increment_removes_the_half_hour_starts()
    {
        var schedule = MakeSchedule(new Dictionary<DayOfWeek, List<TimeRange>>
        {
            [Today.DayOfWeek] = [new TimeRange(480, 600)]
        });
        var court = MakeCourt(durations: [60], startIncrementMinutes: 60);

        var slots = AvailabilityCalculator.SlotsFor(court, schedule, [], [], Today, Today, 0);

        Assert.Equal([480, 540], slots.Select(slot => slot.StartMinute));
    }

    [Fact]
    public void A_duration_that_does_not_fit_before_closing_is_not_offered()
    {
        var schedule = MakeSchedule(new Dictionary<DayOfWeek, List<TimeRange>>
        {
            [Today.DayOfWeek] = [new TimeRange(480, 540)]
        });
        var court = MakeCourt(durations: [60, 90], startIncrementMinutes: 30);

        var slots = AvailabilityCalculator.SlotsFor(court, schedule, [], [], Today, Today, 0);

        Assert.Contains(60, slots.Select(slot => slot.DurationMinutes));
        Assert.DoesNotContain(90, slots.Select(slot => slot.DurationMinutes));
    }

    [Fact]
    public void The_minimum_notice_filters_today_but_not_tomorrow()
    {
        var scheduleToday = MakeSchedule(new Dictionary<DayOfWeek, List<TimeRange>>
        {
            [Today.DayOfWeek] = [new TimeRange(960, 1080)]
        });
        var scheduleTomorrow = MakeSchedule(new Dictionary<DayOfWeek, List<TimeRange>>
        {
            [Tomorrow.DayOfWeek] = [new TimeRange(960, 1080)]
        });
        var court = MakeCourt(durations: [60], startIncrementMinutes: 60, minimumNoticeMinutes: 60);

        var todaySlots = AvailabilityCalculator.SlotsFor(court, scheduleToday, [], [], Today, Today, 1000);
        var tomorrowSlots = AvailabilityCalculator.SlotsFor(court, scheduleTomorrow, [], [], Tomorrow, Today, 1000);

        Assert.DoesNotContain(1020, todaySlots.Select(slot => slot.StartMinute));
        Assert.Contains(1020, tomorrowSlots.Select(slot => slot.StartMinute));
    }

    [Fact]
    public void A_past_date_returns_no_slots()
    {
        var schedule = MakeSchedule();
        var court = MakeCourt();

        var slots = AvailabilityCalculator.SlotsFor(court, schedule, [], [], Today.AddDays(-1), Today, 0);

        Assert.Empty(slots);
    }

    [Fact]
    public void An_inactive_court_returns_no_slots()
    {
        var schedule = MakeSchedule();
        var court = MakeCourt(isActive: false);

        var slots = AvailabilityCalculator.SlotsFor(court, schedule, [], [], Today, Today, 0);

        Assert.Empty(slots);
    }

    [Fact]
    public void A_start_before_the_night_cutoff_uses_the_day_price()
    {
        var schedule = MakeSchedule(new Dictionary<DayOfWeek, List<TimeRange>>
        {
            [Today.DayOfWeek] = [new TimeRange(480, 600)]
        });
        var court = MakeCourt(
            durations: [60], startIncrementMinutes: 30, dayPrice: 100m, nightPrice: 200m, nightStartsAtMinute: 540);

        var slots = AvailabilityCalculator.SlotsFor(court, schedule, [], [], Today, Today, 0);

        var slot = slots.Single(candidate => candidate.StartMinute == 480);
        Assert.Equal(Money.Of(100m, "ARS"), slot.Price);
    }

    [Fact]
    public void A_start_at_or_after_the_night_cutoff_uses_the_night_price()
    {
        var schedule = MakeSchedule(new Dictionary<DayOfWeek, List<TimeRange>>
        {
            [Today.DayOfWeek] = [new TimeRange(480, 600)]
        });
        var court = MakeCourt(
            durations: [60], startIncrementMinutes: 30, dayPrice: 100m, nightPrice: 200m, nightStartsAtMinute: 540);

        var slots = AvailabilityCalculator.SlotsFor(court, schedule, [], [], Today, Today, 0);

        var slot = slots.Single(candidate => candidate.StartMinute == 540);
        Assert.Equal(Money.Of(200m, "ARS"), slot.Price);
    }

    [Fact]
    public void A_90_minute_duration_scales_the_hourly_price_by_1_5x()
    {
        var schedule = MakeSchedule(new Dictionary<DayOfWeek, List<TimeRange>>
        {
            [Today.DayOfWeek] = [new TimeRange(480, 570)]
        });
        var court = MakeCourt(
            durations: [90], startIncrementMinutes: 30, dayPrice: 100m, nightPrice: 200m, nightStartsAtMinute: 1200);

        var slots = AvailabilityCalculator.SlotsFor(court, schedule, [], [], Today, Today, 0);

        var slot = slots.Single(candidate => candidate.StartMinute == 480);
        Assert.Equal(Money.Of(150m, "ARS"), slot.Price);
    }

    [Fact]
    public void Slots_are_ordered_by_start_then_by_duration()
    {
        var schedule = MakeSchedule(new Dictionary<DayOfWeek, List<TimeRange>>
        {
            [Today.DayOfWeek] = [new TimeRange(480, 720)]
        });
        var court = MakeCourt(durations: [90, 60], startIncrementMinutes: 30);

        var slots = AvailabilityCalculator.SlotsFor(court, schedule, [], [], Today, Today, 0);

        Assert.Equal(slots.OrderBy(slot => slot.StartMinute).ThenBy(slot => slot.DurationMinutes), slots);
        Assert.Equal([60, 90], slots.Where(slot => slot.StartMinute == 480).Select(slot => slot.DurationMinutes));
    }

    [Fact]
    public void A_partially_overlapping_confirmed_booking_discards_the_start()
    {
        var schedule = MakeSchedule(new Dictionary<DayOfWeek, List<TimeRange>>
        {
            [Today.DayOfWeek] = [new TimeRange(480, 660)]
        });
        var court = MakeCourt(durations: [60], startIncrementMinutes: 30);
        var booking = MakeBooking(510, 60);

        var slots = AvailabilityCalculator.SlotsFor(court, schedule, [], [booking], Today, Today, 0);

        Assert.Equal([570, 600], slots.Select(slot => slot.StartMinute));
    }

    [Fact]
    public void An_exactly_overlapping_confirmed_booking_discards_the_start()
    {
        var schedule = MakeSchedule(new Dictionary<DayOfWeek, List<TimeRange>>
        {
            [Today.DayOfWeek] = [new TimeRange(600, 720)]
        });
        var court = MakeCourt(durations: [60], startIncrementMinutes: 30);
        var booking = MakeBooking(600, 60);

        var slots = AvailabilityCalculator.SlotsFor(court, schedule, [], [booking], Today, Today, 0);

        Assert.Equal([660], slots.Select(slot => slot.StartMinute));
    }

    [Fact]
    public void A_start_contained_in_a_longer_confirmed_booking_is_discarded()
    {
        var schedule = MakeSchedule(new Dictionary<DayOfWeek, List<TimeRange>>
        {
            [Today.DayOfWeek] = [new TimeRange(480, 780)]
        });
        var court = MakeCourt(durations: [60], startIncrementMinutes: 60);
        var booking = MakeBooking(600, 120);

        var slots = AvailabilityCalculator.SlotsFor(court, schedule, [], [booking], Today, Today, 0);

        Assert.Equal([480, 540, 720], slots.Select(slot => slot.StartMinute));
    }

    [Fact]
    public void Adjacent_bookings_before_and_after_do_not_discard_the_start()
    {
        var schedule = MakeSchedule(new Dictionary<DayOfWeek, List<TimeRange>>
        {
            [Today.DayOfWeek] = [new TimeRange(480, 780)]
        });
        var court = MakeCourt(durations: [60], startIncrementMinutes: 60);
        var before = MakeBooking(540, 60);
        var after = MakeBooking(660, 60);

        var slots = AvailabilityCalculator.SlotsFor(court, schedule, [], [before, after], Today, Today, 0);

        Assert.Equal([480, 600, 720], slots.Select(slot => slot.StartMinute));
    }

    [Fact]
    public void A_live_hold_discards_its_start_exactly_like_a_confirmation()
    {
        var schedule = MakeSchedule(new Dictionary<DayOfWeek, List<TimeRange>>
        {
            [Today.DayOfWeek] = [new TimeRange(600, 660)]
        });
        var court = MakeCourt(durations: [60], startIncrementMinutes: 30);
        var hold = MakeHold(600, 60);

        var slots = AvailabilityCalculator.SlotsFor(court, schedule, [], [hold], Today, Today, 0);

        Assert.Empty(slots);
    }
}
