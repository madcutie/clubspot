using ClubSpot.Api.Auth;
using ClubSpot.Api.Modularity;
using ClubSpot.Domain.Bookings;
using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Modularity;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ClubSpot.Api.Endpoints;

public static class CourtEndpoints
{
    public static IEndpointRouteBuilder MapCourts(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/courts")
            .RequireAuthorization(AuthorizationPolicies.ConfigurationEdit)
            .RequireModule(ModuleId.Bookings);
        group.MapGet("/", GetAsync);
        group.MapPut("/", ReplaceAsync);
        return app;
    }

    private static async Task<IResult> GetAsync(BookingsDbContext db, CancellationToken cancellationToken) =>
        Results.Ok(await db.Courts.AsNoTracking().OrderBy(court => court.Sport).ThenBy(court => court.SortOrder).Select(court => CourtResponse.From(court)).ToListAsync(cancellationToken));

    private static async Task<IResult> ReplaceAsync(IReadOnlyList<CourtRequest> requests, BookingsDbContext db, ITenantContext tenantContext, CancellationToken cancellationToken)
    {
        var scheduleIds = await db.Schedules.Select(schedule => schedule.Id).ToHashSetAsync(cancellationToken);
        if (requests.Any(request => !scheduleIds.Contains(request.ScheduleId))) return Results.UnprocessableEntity();
        var existing = await db.Courts.ToDictionaryAsync(court => court.Id, cancellationToken);
        var incomingIds = requests.Where(request => request.Id is not null).Select(request => request.Id!.Value).ToHashSet();
        db.Courts.RemoveRange(existing.Values.Where(court => !incomingIds.Contains(court.Id)));
        foreach (var request in requests)
        {
            var court = request.ToCourt(request.Id ?? Guid.NewGuid(), tenantContext.Current);
            if (existing.TryGetValue(court.Id, out var current)) db.Entry(current).CurrentValues.SetValues(court);
            else db.Courts.Add(court);
        }
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private sealed record CourtRequest(Guid? Id, Sport Sport, int SortOrder, string Name, string Detail, bool IsCovered, bool IsActive, Guid ScheduleId, int[] Durations, int StartIncrementMinutes, int MinimumNoticeMinutes, decimal DayPrice, decimal NightPrice, int NightStartsAtMinute)
    {
        public Court ToCourt(Guid id, TenantId tenantId) => new(id, tenantId, Sport, SortOrder, Name, Detail, IsCovered, IsActive, ScheduleId, Durations, StartIncrementMinutes, MinimumNoticeMinutes, DayPrice, NightPrice, NightStartsAtMinute);
    }

    private sealed record CourtResponse(Guid Id, Sport Sport, int SortOrder, string Name, string Detail, bool IsCovered, bool IsActive, Guid ScheduleId, int[] Durations, int StartIncrementMinutes, int MinimumNoticeMinutes, decimal DayPrice, decimal NightPrice, int NightStartsAtMinute)
    {
        public static CourtResponse From(Court court) => new(court.Id, court.Sport, court.SortOrder, court.Name, court.Detail, court.IsCovered, court.IsActive, court.ScheduleId, court.Durations, court.StartIncrementMinutes, court.MinimumNoticeMinutes, court.DayPrice, court.NightPrice, court.NightStartsAtMinute);
    }
}
