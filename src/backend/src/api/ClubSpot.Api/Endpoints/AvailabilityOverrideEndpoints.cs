using System.Security.Claims;
using ClubSpot.Api.Auth;
using ClubSpot.Api.Modularity;
using ClubSpot.Application.Bookings;
using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Modularity;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ClubSpot.Api.Endpoints;

public static class AvailabilityOverrideEndpoints
{
    public static IEndpointRouteBuilder MapAvailabilityOverrides(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/availability-overrides")
            .RequireAuthorization(AuthorizationPolicies.ConfigurationEdit)
            .RequireModule(ModuleId.Bookings)
            .WithTags("availabilityOverrides");
        group.MapGet("/", ListAsync).WithName("ListAvailabilityOverrides");
        group.MapPost("/", CreateAsync).WithName("CreateAvailabilityOverride");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteAvailabilityOverride");
        return app;
    }

    private static async Task<Results<Ok<IReadOnlyList<OverrideResponse>>, UnprocessableEntity>> ListAsync(
        DateOnly from, DateOnly to, IAvailabilityOverridesStore store, CancellationToken cancellationToken)
    {
        if (from > to) return TypedResults.UnprocessableEntity();
        var overrides = await store.ListAsync(from, to, cancellationToken);
        return TypedResults.Ok<IReadOnlyList<OverrideResponse>>([.. overrides.Select(OverrideResponse.From)]);
    }

    private static async Task<Results<Created<IdResponse>, UnprocessableEntity, UnauthorizedHttpResult>> CreateAsync(
        OverrideRequest request, HttpContext context, IAvailabilityOverridesStore store, CancellationToken cancellationToken)
    {
        var createdBy = UserId(context.User);
        if (createdBy is null) return TypedResults.Unauthorized();

        var result = await store.CreateAsync(request.ToInput(createdBy.Value), cancellationToken);
        return result.Outcome switch
        {
            OverrideCreateOutcome.Created => TypedResults.Created($"/api/availability-overrides/{result.Id}", new IdResponse(result.Id)),
            OverrideCreateOutcome.UnknownCourt or OverrideCreateOutcome.NoDates or OverrideCreateOutcome.DuplicateDates
                or OverrideCreateOutcome.InvalidWindows or OverrideCreateOutcome.ReasonTooLong => TypedResults.UnprocessableEntity(),
            _ => throw new ArgumentOutOfRangeException(nameof(result.Outcome))
        };
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(Guid id, IAvailabilityOverridesStore store, CancellationToken cancellationToken) =>
        await store.DeleteAsync(id, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();

    private static Guid? UserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var id) ? id : null;

    internal sealed record OverrideWindowRequest(int OpensAtMinute, int ClosesAtMinute)
    {
        public OverrideWindowInput ToInput() => new(OpensAtMinute, ClosesAtMinute);
    }

    internal sealed record OverrideRequest(Guid? CourtId, IReadOnlyList<DateOnly> Dates, IReadOnlyList<OverrideWindowRequest> Windows, string? Reason)
    {
        public OverrideCreateInput ToInput(Guid createdBy) => new(CourtId, Dates, Windows.Select(window => window.ToInput()).ToList(), Reason, createdBy);
    }

    internal sealed record IdResponse(Guid Id);

    internal sealed record OverrideResponse(Guid Id, Guid? CourtId, IReadOnlyList<DateOnly> Dates, IReadOnlyList<TimeRange> Windows,
        string? Reason, DateTimeOffset CreatedAt, Guid CreatedBy)
    {
        public static OverrideResponse From(AvailabilityOverride availabilityOverride) => new(availabilityOverride.Id, availabilityOverride.CourtId,
            availabilityOverride.Dates.Select(date => date.Date).ToList(), availabilityOverride.Windows, availabilityOverride.Reason,
            availabilityOverride.CreatedAt, availabilityOverride.CreatedBy);
    }
}
