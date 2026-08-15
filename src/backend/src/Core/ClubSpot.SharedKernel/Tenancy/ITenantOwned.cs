namespace ClubSpot.SharedKernel.Tenancy;

// The only legitimate entity outside this interface is core.club: it is the tenant registry.
public interface ITenantOwned
{
    TenantId TenantId { get; }
}

public sealed class TenantMismatchException(TenantId expected, TenantId actual)
    : InvalidOperationException(
        $"Operation belongs to tenant '{actual}' but the current scope is '{expected}'.");
