using ClubSpot.Application.Core.Users;
using ClubSpot.Domain.Core;
using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Modularity;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ClubSpot.Api.Seed;

public sealed class DevSeeder(
    CoreDbContext db,
    ModuleCatalog moduleCatalog,
    IPasswordHasher passwordHasher,
    ITenantScopeFactory tenantScopeFactory)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        const string slug = "chaco-for-ever";
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
                DateTimeOffset.UtcNow);
            db.Clubs.Add(club);
            await db.SaveChangesAsync(cancellationToken);
        }

        using var tenantScope = tenantScopeFactory.BeginScope(club.Id);
        var enabledModules = await db.ClubModules.Select(module => module.ModuleId).ToHashSetAsync(cancellationToken);
        var requiredModules = moduleCatalog.Resolve([ModuleId.Members, ModuleId.Padel, ModuleId.Football]);
        db.ClubModules.AddRange(requiredModules
            .Except(enabledModules)
            .Select(module => new ClubModule(club.Id, module, DateTimeOffset.UtcNow)));

        if (!await db.Users.AnyAsync(user => user.Email == "admin@chacoforever.test", cancellationToken))
        {
            db.Users.Add(new User(
                Guid.Parse("db645a8a-62ce-46b1-baeb-883e16bb1e22"),
                club.Id,
                "admin@chacoforever.test",
                "Administrador",
                passwordHasher.Hash("clubspot-dev"),
                [Role.Administrator],
                DateTimeOffset.UtcNow));
        }

        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(cancellationToken);
    }
}
