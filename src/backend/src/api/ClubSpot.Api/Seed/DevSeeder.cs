using ClubSpot.Application.Core.Users;
using ClubSpot.Domain.Bookings;
using ClubSpot.Domain.Core;
using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Modularity;
using ClubSpot.SharedKernel.Primitives;
using ClubSpot.SharedKernel.Tenancy;
using ClubSpot.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace ClubSpot.Api.Seed;

public sealed class DevSeeder(
    ClubSpotDbContext db,
    IPasswordHasher passwordHasher,
    ITenantScopeFactory tenantScopeFactory,
    IClock clock)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        const string slug = "chaco-for-ever";
        var now = clock.UtcNow;
        var club = await db.Clubs.SingleOrDefaultAsync(candidate => candidate.Slug == slug, cancellationToken);
        if (club is null)
        {
            club = new Club(
                TenantId.From(Guid.Parse("a7b00b98-6191-433d-8930-3273904c1faa")),
                slug,
                "Club Atlético Chaco For Ever",
                "Resistencia",
                "America/Argentina/Buenos_Aires",
                "ARS",
                50,
                now);
            db.Clubs.Add(club);
            await db.SaveChangesAsync(cancellationToken);
        }

        using var tenantScope = tenantScopeFactory.BeginScope(club.Id);
        var contractedModules = await db.ClubModules.Select(module => module.ModuleId).ToHashSetAsync(cancellationToken);
        ModuleId[] requiredModules = [ModuleId.Members, ModuleId.Bookings];
        db.ClubModules.AddRange(requiredModules
            .Except(contractedModules)
            .Select(module => new ClubModule(club.Id, module, now)));

        if (!await db.Users.AnyAsync(user => user.Email == "admin@chacoforever.test", cancellationToken))
        {
            db.Users.Add(new User(
                Guid.Parse("db645a8a-62ce-46b1-baeb-883e16bb1e22"),
                club.Id,
                "admin@chacoforever.test",
                "Administrador",
                passwordHasher.Hash("clubspot-dev"),
                [Role.Administrator],
                now));
        }

        if (!await db.Users.AnyAsync(user => user.Email == "reception@chacoforever.test", cancellationToken))
        {
            db.Users.Add(new User(
                Guid.Parse("6f2f0e2c-4a58-4d0b-9a2e-0f7c1d3b5a91"),
                club.Id,
                "reception@chacoforever.test",
                "Canchero",
                passwordHasher.Hash("clubspot-dev"),
                [Role.CourtReception],
                now));
        }

        if (!await db.Schedules.AnyAsync(cancellationToken))
        {
            var baseSchedule = new Schedule(
                Guid.NewGuid(),
                club.Id,
                "Base",
                Enum.GetValues<DayOfWeek>().ToDictionary(day => day, _ => new List<TimeRange> { new(480, 1380) }));
            db.Schedules.Add(baseSchedule);
            db.Courts.AddRange(
                new Court(Guid.NewGuid(), club.Id, Sport.Padel, 1, "Cancha 1", "Blindex · techada", isCovered: true, isActive: true,
                    baseSchedule.Id, [60, 90, 120], 30, 0, Money.Of(14000m, club.Currency), Money.Of(18000m, club.Currency), 1140),
                new Court(Guid.NewGuid(), club.Id, Sport.Padel, 2, "Cancha 2", "Descubierta", isCovered: false, isActive: true,
                    baseSchedule.Id, [60, 90, 120], 30, 0, Money.Of(12000m, club.Currency), Money.Of(16000m, club.Currency), 1140),
                new Court(Guid.NewGuid(), club.Id, Sport.Football, 1, "Fútbol A", "Fútbol 5 · césped sintético", isCovered: false, isActive: true,
                    baseSchedule.Id, [60], 60, 0, Money.Of(30000m, club.Currency), Money.Of(36000m, club.Currency), 1140));
        }

        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(cancellationToken);
    }
}
