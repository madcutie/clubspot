using ClubSpot.Api.Auth;
using ClubSpot.Api.Modularity;
using ClubSpot.Application.Bookings;
using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Modularity;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ClubSpot.Api.Endpoints;

public static class CourtEndpoints
{
    public static IEndpointRouteBuilder MapCourts(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/courts")
            .RequireAuthorization(AuthorizationPolicies.ConfigurationEdit)
            .RequireModule(ModuleId.Bookings)
            .WithTags("courts");
        group.MapGet("/", GetAsync).WithName("GetCourts");
        group.MapPut("/", ReplaceAsync).WithName("ReplaceCourts");
        return app;
    }

    private static async Task<Ok<IReadOnlyList<CourtResponse>>> GetAsync(GetCourtsHandler handler, CancellationToken cancellationToken)
    {
        var snapshots = await handler.HandleAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<CourtResponse>>([.. snapshots.Select(CourtResponse.From)]);
    }

    private static async Task<Results<NoContent, UnprocessableEntity, Conflict>> ReplaceAsync(
        IReadOnlyList<CourtRequest> requests, ReplaceCourtsHandler handler, CancellationToken cancellationToken)
    {
        var outcome = await handler.HandleAsync(requests.Select(request => request.ToInput()).ToList(), cancellationToken);
        return outcome switch
        {
            ReplaceOutcome.Saved => TypedResults.NoContent(),
            ReplaceOutcome.DuplicateIds or ReplaceOutcome.VersionMissing or ReplaceOutcome.VersionUnexpected or ReplaceOutcome.UnknownSchedule => TypedResults.UnprocessableEntity(),
            ReplaceOutcome.VersionConflict or ReplaceOutcome.ScheduleInUse => TypedResults.Conflict(),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
    }

    internal sealed record CourtRequest(Guid? Id, uint? Version, Sport Sport, int SortOrder, string Name, string Detail, bool IsCovered, bool IsActive, Guid ScheduleId, int[] Durations, int StartIncrementMinutes, int MinimumNoticeMinutes, decimal DayPrice, decimal NightPrice, int NightStartsAtMinute)
    {
        public CourtReplaceInput ToInput() => new(Id, Version, Sport, SortOrder, Name, Detail, IsCovered, IsActive, ScheduleId, Durations, StartIncrementMinutes, MinimumNoticeMinutes, DayPrice, NightPrice, NightStartsAtMinute);
    }

    internal sealed record CourtResponse(Guid Id, Sport Sport, int SortOrder, string Name, string Detail, bool IsCovered, bool IsActive, Guid ScheduleId, int[] Durations, int StartIncrementMinutes, int MinimumNoticeMinutes, decimal DayPrice, decimal NightPrice, int NightStartsAtMinute, uint Version)
    {
        public static CourtResponse From(CourtSnapshot snapshot) => new(snapshot.Court.Id, snapshot.Court.Sport, snapshot.Court.SortOrder, snapshot.Court.Name,
            snapshot.Court.Detail, snapshot.Court.IsCovered, snapshot.Court.IsActive, snapshot.Court.ScheduleId, snapshot.Court.Durations,
            snapshot.Court.StartIncrementMinutes, snapshot.Court.MinimumNoticeMinutes, snapshot.Court.DayPrice.Amount, snapshot.Court.NightPrice.Amount,
            snapshot.Court.NightStartsAtMinute, snapshot.Version);
    }
}
