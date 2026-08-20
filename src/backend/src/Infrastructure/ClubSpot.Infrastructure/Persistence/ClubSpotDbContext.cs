using System.Reflection;
using ClubSpot.Domain.Bookings;
using ClubSpot.Domain.Core;
using ClubSpot.Domain.Core.Activity;
using ClubSpot.Domain.Core.People;
using ClubSpot.Infrastructure.Persistence.Configurations;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ClubSpot.Infrastructure.Persistence;

// Tenancy enforcement: a global read filter on ITenantOwned entities and a write guard that
// stamps the tenant on inserts and throws on foreign-tenant rows.
public sealed class ClubSpotDbContext(DbContextOptions<ClubSpotDbContext> options, ITenantContext tenantContext)
    : DbContext(options)
{
    public const string Schema = "public";

    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ClubModule> ClubModules => Set<ClubModule>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<Court> Courts => Set<Court>();
    public DbSet<AvailabilityOverride> AvailabilityOverrides => Set<AvailabilityOverride>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ActivityLogEntry> ActivityLogEntries => Set<ActivityLogEntry>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<TenantId>().HaveConversion<TenantIdConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.HasPostgresExtension("btree_gist");
        modelBuilder.ApplyConfiguration(new ClubConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new ClubModuleConfiguration());
        modelBuilder.ApplyConfiguration(new PersonConfiguration());
        modelBuilder.ApplyConfiguration(new NoteConfiguration());
        modelBuilder.ApplyConfiguration(new ScheduleConfiguration());
        modelBuilder.ApplyConfiguration(new CourtConfiguration());
        modelBuilder.ApplyConfiguration(new AvailabilityOverrideConfiguration());
        modelBuilder.ApplyConfiguration(new AvailabilityOverrideDateConfiguration());
        modelBuilder.ApplyConfiguration(new BookingConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentConfiguration());
        modelBuilder.ApplyConfiguration(new ActivityLogEntryConfiguration());

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                typeof(ClubSpotDbContext)
                    .GetMethod(nameof(ApplyTenantFilter), BindingFlags.Instance | BindingFlags.NonPublic)!
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, [modelBuilder]);
            }
        }

        ApplyPhysicalNamingConvention(modelBuilder);
    }

    // Runs last, over the finished model: EF would otherwise name keys, indexes and foreign keys
    // with its own underscore defaults, which the camelCase convention does not accept.
    private static void ApplyPhysicalNamingConvention(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.GetTableName() is not { } table) continue;
            var target = Pascal(table);

            entityType.FindPrimaryKey()?.SetName($"pk{target}");

            foreach (var index in entityType.GetIndexes())
                index.SetDatabaseName($"{(index.IsUnique ? "ux" : "ix")}{target}{Columns(index.Properties)}");

            foreach (var foreignKey in entityType.GetForeignKeys())
                foreignKey.SetConstraintName($"fk{target}{Columns(foreignKey.Properties)}");
        }

        static string Columns(IEnumerable<IMutableProperty> properties) =>
            string.Concat(properties.Select(property => Pascal(property.GetColumnName())));

        static string Pascal(string value) => char.ToUpperInvariant(value[0]) + value[1..];
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == tenantContext.Current);
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

        var tenant = tenantContext.Current;

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
