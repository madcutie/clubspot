using ClubSpot.Application.Core;
using ClubSpot.SharedKernel.Activity;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Api.Endpoints;

// Shared by every anonymous group whose route carries {clubSlug}: resolves the club and opens
// the tenant scope around the rest of the pipeline. Register it before RequireModule.
internal static class ClubScope
{
    public static ValueTask<object?> ResolveAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        ResolveAsync(context, next, ActivityActor.Portal());

    // The webhook groups pass their own actor: what entered is a provider notification, not a buyer.
    public static async ValueTask<object?> ResolveAsync(EndpointFilterInvocationContext context,
        EndpointFilterDelegate next, ActivityActor actor)
    {
        if (context.HttpContext.Request.RouteValues["clubSlug"] is not string slug) return Results.NotFound();

        var clubDirectory = context.HttpContext.RequestServices.GetRequiredService<IClubDirectory>();
        var clubId = await clubDirectory.FindClubIdBySlugAsync(slug, context.HttpContext.RequestAborted);
        if (clubId is null) return Results.NotFound();

        var tenantScopeFactory = context.HttpContext.RequestServices.GetRequiredService<ITenantScopeFactory>();
        using var tenantScope = tenantScopeFactory.BeginScope(clubId.Value);
        var actorScopeFactory = context.HttpContext.RequestServices.GetRequiredService<IActivityActorScopeFactory>();
        using var actorScope = actorScopeFactory.BeginScope(actor);

        // The result is executed here, not returned to be executed later: a filter returns before the
        // response is written, and both scopes close on the way out. Anything the result still needs
        // from persistence would then run with no tenant and throw far from the cause.
        var result = await next(context);
        if (result is not IResult produced) return result;
        await produced.ExecuteAsync(context.HttpContext);
        return Results.Empty;
    }
}
