namespace ClubSpot.SharedKernel.Tenancy;

// Current throws when no tenant is set instead of returning a neutral value: a background
// process without a scope must blow up, not silently process zero rows or leak across clubs.
public interface ITenantContext
{
    bool HasTenant { get; }

    TenantId Current { get; }
}

public sealed class MissingTenantException(string operation)
    : InvalidOperationException(
        $"No tenant scope is set for operation '{operation}'. " +
        "Background work must open an explicit tenant scope before touching persistence.");
