using ClubSpot.Application.Core;
using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Primitives;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Application.Bookings;

public sealed record ScheduleReplaceInput(Guid? Id, uint? Version, string Name,
    Dictionary<DayOfWeek, List<TimeRange>> WeeklyRanges);

public sealed record CourtReplaceInput(Guid? Id, uint? Version, Sport Sport, int SortOrder, string Name, string Detail,
    bool IsCovered, bool IsActive, Guid ScheduleId, int[] Durations, int StartIncrementMinutes, int MinimumNoticeMinutes,
    decimal DayPrice, decimal NightPrice, int NightStartsAtMinute);

public sealed class GetSchedulesHandler(ISchedulesStore store)
{
    public Task<IReadOnlyList<ScheduleSnapshot>> HandleAsync(CancellationToken cancellationToken) =>
        store.GetAllAsync(cancellationToken);
}

public sealed class ReplaceSchedulesHandler(ISchedulesStore store, ITenantContext tenantContext)
{
    public Task<ReplaceOutcome> HandleAsync(IReadOnlyList<ScheduleReplaceInput> inputs, CancellationToken cancellationToken)
    {
        var explicitIds = inputs.Where(input => input.Id is not null).Select(input => input.Id!.Value).ToHashSet();
        if (explicitIds.Count != inputs.Count(input => input.Id is not null))
            return Task.FromResult(ReplaceOutcome.DuplicateIds);

        var tenant = tenantContext.Current;
        var items = inputs.Select(input => (
            Schedule: new Schedule(input.Id ?? Guid.NewGuid(), tenant, input.Name, input.WeeklyRanges),
            Version: input.Version)).ToList();
        return store.ReplaceAllAsync(items, cancellationToken);
    }
}

public sealed class GetCourtsHandler(ICourtsStore store)
{
    public Task<IReadOnlyList<CourtSnapshot>> HandleAsync(CancellationToken cancellationToken) =>
        store.GetAllAsync(cancellationToken);
}

public sealed class ReplaceCourtsHandler(ICourtsStore store, ITenantContext tenantContext, IClubSettings clubSettings)
{
    public async Task<ReplaceOutcome> HandleAsync(IReadOnlyList<CourtReplaceInput> inputs, CancellationToken cancellationToken)
    {
        var explicitIds = inputs.Where(input => input.Id is not null).Select(input => input.Id!.Value).ToHashSet();
        if (explicitIds.Count != inputs.Count(input => input.Id is not null))
            return ReplaceOutcome.DuplicateIds;

        var tenant = tenantContext.Current;
        var club = await clubSettings.GetAsync(cancellationToken);
        var items = inputs.Select(input => (
            Court: new Court(input.Id ?? Guid.NewGuid(), tenant, input.Sport, input.SortOrder, input.Name, input.Detail,
                input.IsCovered, input.IsActive, input.ScheduleId, input.Durations, input.StartIncrementMinutes,
                input.MinimumNoticeMinutes, Money.Of(input.DayPrice, club.Currency), Money.Of(input.NightPrice, club.Currency), input.NightStartsAtMinute),
            Version: input.Version)).ToList();
        return await store.ReplaceAllAsync(items, cancellationToken);
    }
}
