using System.Security.Claims;
using ClubSpot.Api.Auth;
using ClubSpot.Api.Observability;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Api.Tenancy;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantScopeFactory tenantScopeFactory)
    {
        var tenantValue = context.User.FindFirstValue(ClubSpotClaims.Tenant);
        if (tenantValue is null || !Guid.TryParse(tenantValue, out var tenantId))
        {
            await next(context);
            return;
        }

        using var tenantScope = tenantScopeFactory.BeginScope(TenantId.From(tenantId));
        // On the context and not in a LogContext scope: a scope would be popped while an exception
        // unwinds, and the 500 logged above this middleware is exactly the line that needs the club.
        context.Items[HttpContextEnricher.TenantKey] = tenantId;
        await next(context);
    }
}
