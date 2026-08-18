using ClubSpot.Application.Core;
using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ClubSpot.Infrastructure.Repositories;

internal sealed class ClubDirectory(ClubSpotDbContext db) : IClubDirectory
{
    public async Task<TenantId?> FindClubIdBySlugAsync(string slug, CancellationToken cancellationToken) =>
        await db.Clubs.AsNoTracking()
            .Where(club => club.Slug == slug.Trim())
            .Select(club => (TenantId?)club.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TenantId>> GetAllClubIdsAsync(CancellationToken cancellationToken) =>
        await db.Clubs.AsNoTracking().Select(club => club.Id).ToListAsync(cancellationToken);
}
