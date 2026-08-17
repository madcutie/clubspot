using ClubSpot.Domain.Bookings;

namespace ClubSpot.Application.Bookings;

public sealed record ScheduleSnapshot(Schedule Schedule, uint Version);

public enum ReplaceOutcome { Saved, DuplicateIds, VersionMissing, VersionUnexpected, UnknownSchedule, VersionConflict, ScheduleInUse }

public interface ISchedulesStore
{
    Task<IReadOnlyList<ScheduleSnapshot>> GetAllAsync(CancellationToken cancellationToken);
    Task<ReplaceOutcome> ReplaceAllAsync(IReadOnlyList<(Schedule Schedule, uint? Version)> items, CancellationToken cancellationToken);
}
