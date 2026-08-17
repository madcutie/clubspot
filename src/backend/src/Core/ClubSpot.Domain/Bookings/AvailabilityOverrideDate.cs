using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Domain.Bookings;

public sealed class AvailabilityOverrideDate : ITenantOwned
{
    public Guid OverrideId { get; private set; }
    public TenantId TenantId { get; private set; }
    public DateOnly Date { get; private set; }

    internal AvailabilityOverrideDate(Guid overrideId, TenantId tenantId, DateOnly date)
    {
        OverrideId = overrideId;
        TenantId = tenantId;
        Date = date;
    }

    private AvailabilityOverrideDate()
    {
    }
}
