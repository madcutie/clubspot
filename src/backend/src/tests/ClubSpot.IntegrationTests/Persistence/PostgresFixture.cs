using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ClubSpot.IntegrationTests.Persistence;

// Real PostgreSQL (requires Docker): the invariants that live in the database — constraints,
// exclusion, schemas — do not exist in any in-memory substitute.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public CoreDbContext CreateCoreDbContext(ITenantContext? tenantContext = null)
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
                npgsql.MigrationsHistoryTable(CoreDbContext.MigrationsHistoryTable, CoreDbContext.Schema))
            .Options;

        return new CoreDbContext(options, tenantContext ?? new AsyncLocalTenantContext());
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateCoreDbContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
