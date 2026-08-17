using ClubSpot.Application.Core;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Api.Endpoints;

// Shared by every anonymous group whose route carries {clubSlug}: resolves the club and opens
// the tenant scope around the rest of the pipeline. Register it before RequireModule.
internal static class ClubScope
{
    public static async ValueTask<object?> ResolveAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (context.HttpContext.Request.RouteValues["clubSlug"] is not string slug) return Results.NotFound();

        var clubDirectory = context.HttpContext.RequestServices.GetRequiredService<IClubDirectory>();
        var clubId = await clubDirectory.FindClubIdBySlugAsync(slug, context.HttpContext.RequestAborted);
        if (clubId is null) return Results.NotFound();

        var tenantScopeFactory = context.HttpContext.RequestServices.GetRequiredService<ITenantScopeFactory>();
        using var tenantScope = tenantScopeFactory.BeginScope(clubId.Value);
        return await next(context);
    }
}
