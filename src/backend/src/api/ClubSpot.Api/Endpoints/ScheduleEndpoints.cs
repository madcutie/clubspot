using ClubSpot.Api.Auth;
using ClubSpot.Api.Modularity;
using ClubSpot.Application.Bookings;
using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Modularity;

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

    private static async Task<IResult> GetAsync(GetSchedulesHandler handler, CancellationToken cancellationToken)
    {
        var snapshots = await handler.HandleAsync(cancellationToken);
        return Results.Ok(snapshots.Select(ScheduleResponse.From));
    }

    private static async Task<IResult> ReplaceAsync(IReadOnlyList<ScheduleRequest> requests, ReplaceSchedulesHandler handler, CancellationToken cancellationToken)
    {
        var outcome = await handler.HandleAsync(requests.Select(request => request.ToInput()).ToList(), cancellationToken);
        return outcome switch
        {
            ReplaceOutcome.Saved => Results.NoContent(),
            ReplaceOutcome.DuplicateIds or ReplaceOutcome.VersionMissing or ReplaceOutcome.VersionUnexpected or ReplaceOutcome.UnknownSchedule => Results.UnprocessableEntity(),
            ReplaceOutcome.VersionConflict or ReplaceOutcome.ScheduleInUse => Results.Conflict(),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
    }

    private sealed record ScheduleRequest(Guid? Id, uint? Version, string Name, Dictionary<DayOfWeek, List<TimeRange>> WeeklyRanges)
    {
        public ScheduleReplaceInput ToInput() => new(Id, Version, Name, WeeklyRanges);
    }

    private sealed record ScheduleResponse(Guid Id, string Name, Dictionary<DayOfWeek, List<TimeRange>> WeeklyRanges, uint Version)
    {
        public static ScheduleResponse From(ScheduleSnapshot snapshot) => new(snapshot.Schedule.Id, snapshot.Schedule.Name,
            snapshot.Schedule.WeeklyRanges.ToDictionary(pair => pair.Key, pair => pair.Value.ToList()),
            snapshot.Version);
    }
}
