using ClubSpot.Domain.Core;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ClubSpot.IntegrationTests.Persistence;

[Collection("postgres")]
public sealed class ClubPersistenceTests(PostgresFixture postgres)
{
    private static Club NewClub(string slug) => new(
        TenantId.From(Guid.NewGuid()),
        slug,
        "Club de Prueba",
        "Sede Central",
        "America/Argentina/Buenos_Aires",
        "ARS",
        depositPercent: 50,
        DateTimeOffset.UtcNow);

    [Fact]
    public async Task A_club_round_trips_by_slug()
    {
        var club = NewClub("club-de-prueba");

        await using (var db = postgres.CreateDbContext())
        {
            db.Clubs.Add(club);
            await db.SaveChangesAsync();
        }

        await using (var db = postgres.CreateDbContext())
        {
            var read = await db.Clubs.SingleAsync(c => c.Slug == "club-de-prueba");

            Assert.Equal(club.Id, read.Id);
            Assert.Equal("Club de Prueba", read.Name);
            Assert.Equal(50, read.DepositPercent);
            Assert.Equal("ARS", read.Currency);
        }
    }

    [Fact]
    public async Task Duplicate_slugs_are_rejected()
    {
        await using var db = postgres.CreateDbContext();
        db.Clubs.Add(NewClub("slug-repetido"));
        await db.SaveChangesAsync();

        await using var db2 = postgres.CreateDbContext();
        db2.Clubs.Add(NewClub("slug-repetido"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task The_database_enforces_that_a_deposit_is_half_or_all_of_the_price()
    {
        await using var db = postgres.CreateDbContext();

        // The aggregate rejects this in its constructor; force the value through the change
        // tracker to prove the database enforces it too.
        var club = NewClub("club-invalido");
        db.Clubs.Add(club);
        db.Entry(club).Property(nameof(Club.DepositPercent)).CurrentValue = 30;

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
