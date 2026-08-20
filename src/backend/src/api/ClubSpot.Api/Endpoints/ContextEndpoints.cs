using System.Security.Claims;
using ClubSpot.Application.Core;
using ClubSpot.Domain.Core;
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

    private static async Task<Ok<ContextResponse>> GetAsync(IClubSettings clubSettings, ITenantModules modules, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var club = await clubSettings.GetAsync(cancellationToken);
        var roles = user.FindAll(ClaimTypes.Role).Select(claim => Enum.Parse<Role>(claim.Value)).ToList();
        var operatorInfo = new OperatorResponse(user.Identity?.Name ?? string.Empty, roles);
        return TypedResults.Ok(new ContextResponse(
            new ClubResponse(club.Name, club.Venue),
            operatorInfo,
            [.. modules.Enabled.Select(module => module.Value).OrderBy(module => module)]));
    }

    internal sealed record ClubResponse(string Name, string? Venue);
    internal sealed record OperatorResponse(string Name, IReadOnlyCollection<Role> Roles);
    internal sealed record ContextResponse(ClubResponse Club, OperatorResponse Operator, IReadOnlyList<string> Modules);
}
