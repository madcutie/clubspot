using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Domain.Bookings;

public sealed class Schedule : ITenantOwned
{
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; }
    public IReadOnlyDictionary<DayOfWeek, IReadOnlyList<TimeRange>> WeeklyRanges { get; private set; }

    public Schedule(Guid id, TenantId tenantId, string name, Dictionary<DayOfWeek, List<TimeRange>> weeklyRanges)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Schedule name cannot be empty.", nameof(name));
        foreach (var ranges in weeklyRanges.Values) TimeRangeRules.EnsureNoOverlaps(ranges);

        var weeklyRangesCopy = new Dictionary<DayOfWeek, IReadOnlyList<TimeRange>>();
        foreach (var (day, ranges) in weeklyRanges) weeklyRangesCopy[day] = ranges.ToList();

        Id = id;
        TenantId = tenantId;
        Name = name.Trim();
        WeeklyRanges = weeklyRangesCopy;
    }

    private Schedule()
    {
        Name = null!;
        WeeklyRanges = null!;
    }
}
