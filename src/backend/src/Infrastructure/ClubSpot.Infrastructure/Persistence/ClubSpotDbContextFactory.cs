using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClubSpot.Infrastructure.Persistence;

// Design-time only (dotnet ef).
internal sealed class ClubSpotDbContextFactory : IDesignTimeDbContextFactory<ClubSpotDbContext>
{
    public ClubSpotDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CLUBSPOT_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=clubspot;Username=postgres;Password=clubspot";

        var options = new DbContextOptionsBuilder<ClubSpotDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ClubSpotDbContext(options, new AsyncLocalTenantContext());
    }
}
