namespace ClubSpot.Domain.Bookings;

public sealed record SpecialDate(DateOnly Date, IReadOnlyCollection<TimeRange> TimeRanges);
