using System.Security.Claims;
using ClubSpot.Api.Auth;
using ClubSpot.SharedKernel.Tenancy;
using Serilog.Context;

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
        // Every log line written from here on names its club. With one club it is noise; with two it
        // is the difference between reading a log and guessing.
        using var logScope = LogContext.PushProperty("tenant", tenantId);
        await next(context);
    }
}
