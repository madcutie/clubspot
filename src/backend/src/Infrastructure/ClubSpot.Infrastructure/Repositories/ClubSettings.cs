using ClubSpot.Application.Core;
using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ClubSpot.Infrastructure.Repositories;

internal sealed class ClubSettings(ClubSpotDbContext db, ITenantContext tenantContext) : IClubSettings
{
    public async Task<ClubInfo> GetAsync(CancellationToken cancellationToken)
    {
        var club = await db.Clubs.AsNoTracking().SingleAsync(club => club.Id == tenantContext.Current, cancellationToken);
        return new ClubInfo(club.Name, club.Venue, club.TimeZone, club.Currency, club.DepositPercent);
    }
}
