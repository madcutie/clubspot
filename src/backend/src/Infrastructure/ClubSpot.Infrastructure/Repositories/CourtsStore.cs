using ClubSpot.Application.Bookings;
using ClubSpot.Domain.Bookings;
using ClubSpot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClubSpot.Infrastructure.Repositories;

internal sealed class CourtsStore(ClubSpotDbContext db) : ICourtsStore
{
    public async Task<IReadOnlyList<CourtSnapshot>> GetAllAsync(CancellationToken cancellationToken) =>
        await db.Courts.AsNoTracking().OrderBy(court => court.Sport).ThenBy(court => court.SortOrder)
            .Select(court => new CourtSnapshot(court, EF.Property<uint>(court, "xmin")))
            .ToListAsync(cancellationToken);

    public async Task<ReplaceOutcome> ReplaceAllAsync(IReadOnlyList<(Court Court, uint? Version)> items, CancellationToken cancellationToken)
    {
        var scheduleIds = await db.Schedules.Select(schedule => schedule.Id).ToHashSetAsync(cancellationToken);
        if (items.Any(item => !scheduleIds.Contains(item.Court.ScheduleId))) return ReplaceOutcome.UnknownSchedule;

        var existing = await db.Courts.ToDictionaryAsync(court => court.Id, cancellationToken);
        var incomingIds = items.Select(item => item.Court.Id).ToHashSet();
        db.Courts.RemoveRange(existing.Values.Where(court => !incomingIds.Contains(court.Id)));

        foreach (var (court, version) in items)
        {
            if (existing.TryGetValue(court.Id, out var current))
            {
                if (version is null) return ReplaceOutcome.VersionMissing;
                db.Entry(current).Property<uint>("xmin").OriginalValue = version.Value;
                db.Entry(current).CurrentValues.SetValues(court);
            }
            else
            {
                if (version is not null) return ReplaceOutcome.VersionUnexpected;
                db.Courts.Add(court);
            }
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return ReplaceOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            return ReplaceOutcome.VersionConflict;
        }
    }
}
