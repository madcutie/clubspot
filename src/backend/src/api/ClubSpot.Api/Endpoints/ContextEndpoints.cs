using ClubSpot.Application.Core;
using ClubSpot.SharedKernel.Modularity;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ClubSpot.Api.Endpoints;

public static class ContextEndpoints
{
    public static IEndpointRouteBuilder MapContext(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/context", GetAsync).RequireAuthorization().WithName("GetContext").WithTags("context");
        return app;
    }

    // Who the operator is comes from the token the caller already has; this is what only the server
    // knows: the club and what it contracted (ADR-0018).
    private static async Task<Ok<ContextResponse>> GetAsync(IClubSettings clubSettings, ITenantModules modules, CancellationToken cancellationToken)
    {
        var club = await clubSettings.GetAsync(cancellationToken);
        return TypedResults.Ok(new ContextResponse(
            new ClubResponse(club.Name, club.Venue),
            [.. modules.Enabled.Select(module => module.Value).OrderBy(module => module)]));
    }

    internal sealed record ClubResponse(string Name, string? Venue);
    internal sealed record ContextResponse(ClubResponse Club, IReadOnlyList<string> Modules);
}
