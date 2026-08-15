using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClubSpot.Infrastructure.Persistence;

// Design-time only (dotnet ef).
internal sealed class CoreDbContextFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    public CoreDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CLUBSPOT_CONNECTION")
            ?? "Host=localhost;Database=clubspot;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(CoreDbContext.MigrationsHistoryTable, CoreDbContext.Schema))
            .Options;

        return new CoreDbContext(options, new AsyncLocalTenantContext());
    }
}
