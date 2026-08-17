using ClubSpot.Api.Modularity;
using ClubSpot.Application.Bookings;
using ClubSpot.Application.Core;
using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Modularity;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Api.Endpoints;

public static class PortalEndpoints
{
    public static IEndpointRouteBuilder MapPortal(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/portal/{clubSlug}")
            .AllowAnonymous();
        // Mandatory order: the slug filter opens the tenant scope that RequireModule needs.
        group.AddEndpointFilter(ResolveClubScopeAsync);
        group.RequireModule(ModuleId.Bookings);
        group.MapGet("/catalog", GetCatalogAsync);
        group.MapGet("/availability", GetAvailabilityAsync);
        return app;
    }

    private static async ValueTask<object?> ResolveClubScopeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (context.HttpContext.Request.RouteValues["clubSlug"] is not string slug) return Results.NotFound();

        var clubDirectory = context.HttpContext.RequestServices.GetRequiredService<IClubDirectory>();
        var clubId = await clubDirectory.FindClubIdBySlugAsync(slug, context.HttpContext.RequestAborted);
        if (clubId is null) return Results.NotFound();

        var tenantScopeFactory = context.HttpContext.RequestServices.GetRequiredService<ITenantScopeFactory>();
        using var tenantScope = tenantScopeFactory.BeginScope(clubId.Value);
        return await next(context);
    }

    private static async Task<IResult> GetCatalogAsync(GetPortalCatalogHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.HandleAsync(cancellationToken));

    private static async Task<IResult> GetAvailabilityAsync(string sport, DateOnly from, DateOnly to,
        GetPortalAvailabilityHandler handler, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Sport>(sport, ignoreCase: true, out var parsedSport)) return Results.BadRequest();

        var result = await handler.HandleAsync(parsedSport, from, to, cancellationToken);
        return result.Outcome switch
        {
            PortalAvailabilityOutcome.Ok => Results.Ok(result.Availability),
            PortalAvailabilityOutcome.RangeTooLong => Results.UnprocessableEntity(),
            _ => throw new ArgumentOutOfRangeException(nameof(result.Outcome))
        };
    }
}
