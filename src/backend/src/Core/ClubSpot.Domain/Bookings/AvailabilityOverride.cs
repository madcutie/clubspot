using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Domain.Bookings;

public sealed class AvailabilityOverride : ITenantOwned
{
    private readonly List<AvailabilityOverrideDate> _dates = [];

    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Guid? CourtId { get; private set; }
    public IReadOnlyList<AvailabilityOverrideDate> Dates => _dates;
    public IReadOnlyList<TimeRange> Windows { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    public AvailabilityOverride(Guid id, TenantId tenantId, Guid? courtId, IReadOnlyList<DateOnly> dates,
        IReadOnlyList<TimeRange> windows, string? reason, DateTimeOffset createdAt, Guid createdBy)
    {
        if (dates.Count == 0) throw new ArgumentException("An availability override must have at least one date.", nameof(dates));
        if (dates.Distinct().Count() != dates.Count) throw new ArgumentException("Availability override dates cannot be duplicated.", nameof(dates));
        TimeRangeRules.EnsureNoOverlaps(windows);

        var trimmedReason = reason?.Trim();
        if (!string.IsNullOrEmpty(trimmedReason) && trimmedReason.Length > 200)
            throw new ArgumentOutOfRangeException(nameof(reason), "Reason cannot exceed 200 characters.");

        Id = id;
        TenantId = tenantId;
        CourtId = courtId;
        Windows = windows.ToList();
        Reason = string.IsNullOrEmpty(trimmedReason) ? null : trimmedReason;
        CreatedAt = createdAt;
        CreatedBy = createdBy;

        foreach (var date in dates.Order())
            _dates.Add(new AvailabilityOverrideDate(id, tenantId, date));
    }

    private AvailabilityOverride()
    {
        Windows = null!;
    }
}
