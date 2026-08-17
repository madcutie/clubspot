using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Application.Core;

public interface IClubDirectory
{
    Task<TenantId?> FindClubIdBySlugAsync(string slug, CancellationToken cancellationToken);
}
