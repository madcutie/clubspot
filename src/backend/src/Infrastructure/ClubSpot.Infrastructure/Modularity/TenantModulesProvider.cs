using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Modularity;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ClubSpot.Infrastructure.Modularity;

internal sealed class TenantModulesProvider(
    CoreDbContext db,
    ITenantContext tenantContext,
    IMemoryCache cache) : ITenantModules
{
    public IReadOnlySet<ModuleId> Enabled => GetEnabled();

    public bool IsEnabled(ModuleId module) => GetEnabled().Contains(module);

    private IReadOnlySet<ModuleId> GetEnabled()
    {
        var tenant = tenantContext.Current;
        return cache.GetOrCreate($"club-modules:{tenant.Value}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            return db.ClubModules.AsNoTracking().Select(module => module.ModuleId).ToHashSet();
        })!;
    }
}
