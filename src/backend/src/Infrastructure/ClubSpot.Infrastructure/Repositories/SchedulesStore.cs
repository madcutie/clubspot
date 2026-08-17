using ClubSpot.Application.Bookings;
using ClubSpot.Domain.Bookings;
using ClubSpot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClubSpot.Infrastructure.Repositories;

internal sealed class SchedulesStore(ClubSpotDbContext db) : ISchedulesStore
{
    public async Task<IReadOnlyList<ScheduleSnapshot>> GetAllAsync(CancellationToken cancellationToken) =>
        await db.Schedules.AsNoTracking().OrderBy(schedule => schedule.Name)
            .Select(schedule => new ScheduleSnapshot(schedule, EF.Property<uint>(schedule, "xmin")))
            .ToListAsync(cancellationToken);

    public async Task<ReplaceOutcome> ReplaceAllAsync(IReadOnlyList<(Schedule Schedule, uint? Version)> items, CancellationToken cancellationToken)
    {
        var existing = await db.Schedules.ToDictionaryAsync(schedule => schedule.Id, cancellationToken);
        var incomingIds = items.Select(item => item.Schedule.Id).ToHashSet();
        var removedIds = existing.Keys.Where(id => !incomingIds.Contains(id)).ToHashSet();
        if (removedIds.Count > 0 && await db.Courts.AnyAsync(court => removedIds.Contains(court.ScheduleId), cancellationToken))
            return ReplaceOutcome.ScheduleInUse;
        db.Schedules.RemoveRange(existing.Values.Where(schedule => removedIds.Contains(schedule.Id)));

        foreach (var (schedule, version) in items)
        {
            if (existing.TryGetValue(schedule.Id, out var current))
            {
                if (version is null) return ReplaceOutcome.VersionMissing;
                db.Entry(current).Property<uint>("xmin").OriginalValue = version.Value;
                db.Entry(current).CurrentValues.SetValues(schedule);
            }
            else
            {
                if (version is not null) return ReplaceOutcome.VersionUnexpected;
                db.Schedules.Add(schedule);
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
