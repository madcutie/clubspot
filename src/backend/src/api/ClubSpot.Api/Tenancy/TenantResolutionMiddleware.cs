using System.Security.Claims;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Api.Tenancy;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantScopeFactory tenantScopeFactory)
    {
        var tenantValue = context.User.FindFirstValue("tenant");
        if (tenantValue is null || !Guid.TryParse(tenantValue, out var tenantId))
        {
            await next(context);
            return;
        }

        using var tenantScope = tenantScopeFactory.BeginScope(TenantId.From(tenantId));
        await next(context);
    }
}
