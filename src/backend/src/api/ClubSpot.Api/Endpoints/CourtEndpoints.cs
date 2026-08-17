using ClubSpot.Api.Auth;
using ClubSpot.Api.Modularity;
using ClubSpot.Application.Bookings;
using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Modularity;

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

    private static async Task<IResult> GetAsync(GetCourtsHandler handler, CancellationToken cancellationToken)
    {
        var snapshots = await handler.HandleAsync(cancellationToken);
        return Results.Ok(snapshots.Select(CourtResponse.From));
    }

    private static async Task<IResult> ReplaceAsync(IReadOnlyList<CourtRequest> requests, ReplaceCourtsHandler handler, CancellationToken cancellationToken)
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

    private sealed record CourtRequest(Guid? Id, uint? Version, Sport Sport, int SortOrder, string Name, string Detail, bool IsCovered, bool IsActive, Guid ScheduleId, int[] Durations, int StartIncrementMinutes, int MinimumNoticeMinutes, decimal DayPrice, decimal NightPrice, int NightStartsAtMinute)
    {
        public CourtReplaceInput ToInput() => new(Id, Version, Sport, SortOrder, Name, Detail, IsCovered, IsActive, ScheduleId, Durations, StartIncrementMinutes, MinimumNoticeMinutes, DayPrice, NightPrice, NightStartsAtMinute);
    }

    private sealed record CourtResponse(Guid Id, Sport Sport, int SortOrder, string Name, string Detail, bool IsCovered, bool IsActive, Guid ScheduleId, int[] Durations, int StartIncrementMinutes, int MinimumNoticeMinutes, decimal DayPrice, decimal NightPrice, int NightStartsAtMinute, uint Version)
    {
        public static CourtResponse From(CourtSnapshot snapshot) => new(snapshot.Court.Id, snapshot.Court.Sport, snapshot.Court.SortOrder, snapshot.Court.Name,
            snapshot.Court.Detail, snapshot.Court.IsCovered, snapshot.Court.IsActive, snapshot.Court.ScheduleId, snapshot.Court.Durations,
            snapshot.Court.StartIncrementMinutes, snapshot.Court.MinimumNoticeMinutes, snapshot.Court.DayPrice.Amount, snapshot.Court.NightPrice.Amount,
            snapshot.Court.NightStartsAtMinute, snapshot.Version);
    }
}
