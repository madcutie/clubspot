using ClubSpot.Application.Bookings;
using ClubSpot.Domain.Bookings;
using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Tenancy;
using ClubSpot.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace ClubSpot.Infrastructure.Repositories;

internal sealed class AvailabilityOverridesStore(ClubSpotDbContext db, ITenantContext tenantContext, IClock clock) : IAvailabilityOverridesStore
{
    public async Task<IReadOnlyList<AvailabilityOverride>> ListAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await db.AvailabilityOverrides.AsNoTracking()
            .Where(availabilityOverride => availabilityOverride.Dates.Any(date => date.Date >= from && date.Date <= to))
            .Include(availabilityOverride => availabilityOverride.Dates)
            .OrderByDescending(availabilityOverride => availabilityOverride.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<OverrideCreateResult> CreateAsync(OverrideCreateInput input, CancellationToken cancellationToken)
    {
        if (input.Dates.Count == 0) return new OverrideCreateResult(OverrideCreateOutcome.NoDates, Guid.Empty);
        if (input.Dates.Distinct().Count() != input.Dates.Count) return new OverrideCreateResult(OverrideCreateOutcome.DuplicateDates, Guid.Empty);

        List<TimeRange> windows;
        try
        {
            windows = input.Windows.Select(window => new TimeRange(window.OpensAtMinute, window.ClosesAtMinute)).ToList();
        }
        catch (ArgumentException)
        {
            return new OverrideCreateResult(OverrideCreateOutcome.InvalidWindows, Guid.Empty);
        }
        if (HasOverlaps(windows)) return new OverrideCreateResult(OverrideCreateOutcome.InvalidWindows, Guid.Empty);

        var trimmedReason = input.Reason?.Trim();
        if (!string.IsNullOrEmpty(trimmedReason) && trimmedReason.Length > 200)
            return new OverrideCreateResult(OverrideCreateOutcome.ReasonTooLong, Guid.Empty);

        if (input.CourtId is { } courtId && !await db.Courts.AnyAsync(court => court.Id == courtId, cancellationToken))
            return new OverrideCreateResult(OverrideCreateOutcome.UnknownCourt, Guid.Empty);

        var availabilityOverride = new AvailabilityOverride(Guid.NewGuid(), tenantContext.Current, input.CourtId,
            input.Dates, windows, input.Reason, clock.UtcNow, input.CreatedBy);
        db.AvailabilityOverrides.Add(availabilityOverride);
        await db.SaveChangesAsync(cancellationToken);
        return new OverrideCreateResult(OverrideCreateOutcome.Created, availabilityOverride.Id);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var availabilityOverride = await db.AvailabilityOverrides.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (availabilityOverride is null) return false;
        db.AvailabilityOverrides.Remove(availabilityOverride);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static bool HasOverlaps(IReadOnlyList<TimeRange> windows)
    {
        var ordered = windows.OrderBy(window => window.OpensAtMinute).ToArray();
        return ordered.Zip(ordered.Skip(1)).Any(pair => pair.First.ClosesAtMinute > pair.Second.OpensAtMinute);
    }
}
