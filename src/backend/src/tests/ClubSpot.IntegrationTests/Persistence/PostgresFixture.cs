using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ClubSpot.IntegrationTests.Persistence;

// Real PostgreSQL (requires Docker): the invariants that live in the database do not exist in
// any in-memory substitute.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public ClubSpotDbContext CreateDbContext(ITenantContext? tenantContext = null)
    {
        var options = new DbContextOptionsBuilder<ClubSpotDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new ClubSpotDbContext(options, tenantContext ?? new AsyncLocalTenantContext());
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
