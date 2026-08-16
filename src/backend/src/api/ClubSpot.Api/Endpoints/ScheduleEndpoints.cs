using ClubSpot.Api.Auth;
using ClubSpot.Api.Modularity;
using ClubSpot.Domain.Bookings;
using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Modularity;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ClubSpot.Api.Endpoints;

public static class ScheduleEndpoints
{
    public static IEndpointRouteBuilder MapSchedules(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/schedules")
            .RequireAuthorization(AuthorizationPolicies.ConfigurationEdit)
            .RequireModule(ModuleId.Bookings);
        group.MapGet("/", GetAsync);
        group.MapPut("/", ReplaceAsync);
        return app;
    }

    private static async Task<IResult> GetAsync(BookingsDbContext db, CancellationToken cancellationToken) =>
        Results.Ok(await db.Schedules.AsNoTracking().OrderBy(schedule => schedule.Name).Select(schedule => ScheduleResponse.From(schedule)).ToListAsync(cancellationToken));

    private static async Task<IResult> ReplaceAsync(IReadOnlyList<ScheduleRequest> requests, BookingsDbContext db, ITenantContext tenantContext, CancellationToken cancellationToken)
    {
        var tenant = tenantContext.Current;
        var existing = await db.Schedules.ToDictionaryAsync(schedule => schedule.Id, cancellationToken);
        var incomingIds = requests.Where(request => request.Id is not null).Select(request => request.Id!.Value).ToHashSet();
        var removedIds = existing.Keys.Where(id => !incomingIds.Contains(id)).ToHashSet();
        if (removedIds.Count > 0 && await db.Courts.AnyAsync(court => removedIds.Contains(court.ScheduleId), cancellationToken))
            return Results.Conflict();
        db.Schedules.RemoveRange(existing.Values.Where(schedule => removedIds.Contains(schedule.Id)));

        foreach (var request in requests)
        {
            var schedule = request.ToSchedule(request.Id ?? Guid.NewGuid(), tenant);
            if (existing.TryGetValue(schedule.Id, out var current)) db.Entry(current).CurrentValues.SetValues(schedule);
            else db.Schedules.Add(schedule);
        }
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private sealed record ScheduleRequest(Guid? Id, string Name, string TimeZone, Dictionary<DayOfWeek, List<TimeRange>> WeeklyRanges, List<SpecialDate> SpecialDates)
    {
        public Schedule ToSchedule(Guid id, TenantId tenantId) => new(id, tenantId, Name, TimeZone, WeeklyRanges, SpecialDates);
    }

    private sealed record ScheduleResponse(Guid Id, string Name, string TimeZone, Dictionary<DayOfWeek, List<TimeRange>> WeeklyRanges, List<SpecialDate> SpecialDates)
    {
        public static ScheduleResponse From(Schedule schedule) => new(schedule.Id, schedule.Name, schedule.TimeZone, schedule.WeeklyRanges, schedule.SpecialDates);
    }
}
