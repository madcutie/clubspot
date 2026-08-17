using ClubSpot.Domain.Bookings;

namespace ClubSpot.Application.Bookings;

public sealed record CourtSnapshot(Court Court, uint Version);

public interface ICourtsStore
{
    Task<IReadOnlyList<CourtSnapshot>> GetAllAsync(CancellationToken cancellationToken);
    Task<ReplaceOutcome> ReplaceAllAsync(IReadOnlyList<(Court Court, uint? Version)> items, CancellationToken cancellationToken);
}
