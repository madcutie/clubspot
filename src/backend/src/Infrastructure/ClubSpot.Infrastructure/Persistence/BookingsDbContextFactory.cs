using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClubSpot.Infrastructure.Persistence;

internal sealed class BookingsDbContextFactory : IDesignTimeDbContextFactory<BookingsDbContext>
{
    public BookingsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUBSPOT_CONNECTION") ?? "Host=localhost;Database=clubspot;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<BookingsDbContext>().UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(BookingsDbContext.MigrationsHistoryTable, BookingsDbContext.Schema)).Options;
        return new BookingsDbContext(options, new AsyncLocalTenantContext());
    }
}
