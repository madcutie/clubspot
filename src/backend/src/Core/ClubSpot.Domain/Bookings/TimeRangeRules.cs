namespace ClubSpot.Domain.Bookings;

internal static class TimeRangeRules
{
    public static void EnsureNoOverlaps(IEnumerable<TimeRange> ranges)
    {
        var ordered = ranges.OrderBy(range => range.OpensAtMinute).ToArray();
        if (ordered.Zip(ordered.Skip(1)).Any(pair => pair.First.ClosesAtMinute > pair.Second.OpensAtMinute))
            throw new ArgumentException("Time ranges cannot overlap.");
    }
}
