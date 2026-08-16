using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Domain.Bookings;

public sealed class Schedule : ITenantOwned
{
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; }
    public string TimeZone { get; private set; }
    public Dictionary<DayOfWeek, List<TimeRange>> WeeklyRanges { get; private set; }
    public List<SpecialDate> SpecialDates { get; private set; }

    public Schedule(Guid id, TenantId tenantId, string name, string timeZone, Dictionary<DayOfWeek, List<TimeRange>> weeklyRanges, List<SpecialDate> specialDates)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Schedule name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(timeZone)) throw new ArgumentException("Time zone cannot be empty.", nameof(timeZone));
        ValidateRanges(weeklyRanges.Values.SelectMany(ranges => ranges));
        if (specialDates.Select(specialDate => specialDate.Date).Distinct().Count() != specialDates.Count)
            throw new ArgumentException("Special dates must be unique.", nameof(specialDates));
        foreach (var specialDate in specialDates) ValidateRanges(specialDate.TimeRanges);
        Id = id;
        TenantId = tenantId;
        Name = name.Trim();
        TimeZone = timeZone;
        WeeklyRanges = weeklyRanges;
        SpecialDates = specialDates;
    }

    private static void ValidateRanges(IEnumerable<TimeRange> ranges)
    {
        var ordered = ranges.OrderBy(range => range.OpensAtMinute).ToArray();
        foreach (var range in ordered) range.Validate();
        if (ordered.Zip(ordered.Skip(1)).Any(pair => pair.First.ClosesAtMinute > pair.Second.OpensAtMinute))
            throw new ArgumentException("Time ranges cannot overlap.");
    }

    private Schedule()
    {
        Name = null!;
        TimeZone = null!;
        WeeklyRanges = null!;
        SpecialDates = null!;
    }
}
