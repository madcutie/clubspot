using ClubSpot.SharedKernel.Modularity;

namespace ClubSpot.Api.Modularity;

public static class RequireModuleFilter
{
    public static RouteGroupBuilder RequireModule(this RouteGroupBuilder group, ModuleId module)
    {
        group.AddEndpointFilter(async (context, next) =>
        {
            var modules = context.HttpContext.RequestServices.GetRequiredService<ITenantModules>();
            if (!modules.IsEnabled(module)) return Results.NotFound();
            return await next(context);
        });
        return group;
    }
}
