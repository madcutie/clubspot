using System.Reflection;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ClubSpot.Infrastructure.Persistence;

// Tenancy enforcement for every context: a global read filter on ITenantOwned entities and a
// write guard that stamps the tenant on inserts and throws on foreign-tenant rows.
// Derived contexts must call base.OnModelCreating LAST, on the completed model.
public abstract class ModuleDbContextBase(DbContextOptions options, ITenantContext tenantContext)
    : DbContext(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;

    protected TenantId CurrentTenant => _tenantContext.Current;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<TenantId>().HaveConversion<TenantIdConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                typeof(ModuleDbContextBase)
                    .GetMethod(nameof(ApplyTenantFilter), BindingFlags.Instance | BindingFlags.NonPublic)!
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, [modelBuilder]);
            }
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == CurrentTenant);
        modelBuilder.Entity<TEntity>().HasIndex(e => e.TenantId);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceTenantOnWrites();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceTenantOnWrites();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnforceTenantOnWrites()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is ITenantOwned &&
                        e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (entries.Count == 0) return;

        var tenant = _tenantContext.Current;

        foreach (var entry in entries)
        {
            var owned = (ITenantOwned)entry.Entity;

            if (entry.State == EntityState.Added && owned.TenantId == default)
            {
                entry.Property(nameof(ITenantOwned.TenantId)).CurrentValue = tenant;
                continue;
            }

            if (owned.TenantId != tenant)
                throw new TenantMismatchException(tenant, owned.TenantId);
        }
    }
}
