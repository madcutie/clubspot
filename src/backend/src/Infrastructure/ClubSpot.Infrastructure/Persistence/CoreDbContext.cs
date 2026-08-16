using ClubSpot.Domain.Core;
using ClubSpot.Domain.Core.People;
using ClubSpot.Infrastructure.Persistence.Configurations;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ClubSpot.Infrastructure.Persistence;

public sealed class CoreDbContext(DbContextOptions<CoreDbContext> options, ITenantContext tenantContext)
    : ModuleDbContextBase(options, tenantContext)
{
    public const string Schema = "core";
    public const string MigrationsHistoryTable = "__EFMigrationsHistory";

    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ClubModule> ClubModules => Set<ClubModule>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new ClubConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new ClubModuleConfiguration());
        modelBuilder.ApplyConfiguration(new PersonConfiguration());
        modelBuilder.ApplyConfiguration(new NoteConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
