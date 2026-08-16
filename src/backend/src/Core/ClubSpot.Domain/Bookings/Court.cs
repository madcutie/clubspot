using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Domain.Bookings;

public sealed class Court : ITenantOwned
{
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Sport Sport { get; private set; }
    public int SortOrder { get; private set; }
    public string Name { get; private set; }
    public string Detail { get; private set; }
    public bool IsCovered { get; private set; }
    public bool IsActive { get; private set; }
    public Guid ScheduleId { get; private set; }
    public int[] Durations { get; private set; }
    public int StartIncrementMinutes { get; private set; }
    public int MinimumNoticeMinutes { get; private set; }
    public decimal DayPrice { get; private set; }
    public decimal NightPrice { get; private set; }
    public int NightStartsAtMinute { get; private set; }

    public Court(Guid id, TenantId tenantId, Sport sport, int sortOrder, string name, string detail, bool isCovered, bool isActive, Guid scheduleId, int[] durations, int startIncrementMinutes, int minimumNoticeMinutes, decimal dayPrice, decimal nightPrice, int nightStartsAtMinute)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Court name cannot be empty.", nameof(name));
        if (durations.Length == 0 || durations.Any(duration => duration <= 0)) throw new ArgumentException("A court must have at least one positive duration.", nameof(durations));
        if (startIncrementMinutes <= 0 || minimumNoticeMinutes < 0 || dayPrice < 0 || nightPrice < 0 || nightStartsAtMinute is < 0 or > 1440)
            throw new ArgumentException("Court configuration is invalid.");
        Id = id; TenantId = tenantId; Sport = sport; SortOrder = sortOrder; Name = name.Trim(); Detail = detail.Trim(); IsCovered = isCovered; IsActive = isActive;
        ScheduleId = scheduleId; Durations = durations.Distinct().Order().ToArray(); StartIncrementMinutes = startIncrementMinutes; MinimumNoticeMinutes = minimumNoticeMinutes;
        DayPrice = dayPrice; NightPrice = nightPrice; NightStartsAtMinute = nightStartsAtMinute;
    }

    private Court()
    {
        Name = null!; Detail = null!; Durations = null!;
    }
}
