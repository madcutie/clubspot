using System.Security.Claims;
using ClubSpot.Api.Auth;
using ClubSpot.SharedKernel.Activity;

namespace ClubSpot.Api.Activity;

// An authenticated request is the counter: the operator in front of the screen. Anonymous surfaces
// open their own scope, so nothing here guesses an actor for them — without a scope the log throws,
// which is the point (ADR-0017).
public sealed class ActivityActorMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IActivityActorScopeFactory actorScopeFactory)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var userId = Guid.TryParse(context.User.FindFirstValue(ClubSpotClaims.Subject), out var parsed)
            ? parsed
            : (Guid?)null;
        var name = context.User.Identity.Name is { Length: > 0 } identity ? identity : ActivityActor.SystemName;

        using var actorScope = actorScopeFactory.BeginScope(
            new ActivityActor(userId, name, ActivitySource.Counter));
        await next(context);
    }
}
