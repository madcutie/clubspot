using ClubSpot.SharedKernel.Modularity;

namespace ClubSpot.Api.Endpoints;

public static class ContextEndpoints
{
    public static IEndpointRouteBuilder MapContext(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/context", (ITenantModules modules) =>
            Results.Ok(new ContextResponse(modules.Enabled.Select(module => module.Value).OrderBy(module => module))))
            .RequireAuthorization();
        return app;
    }

    private sealed record ContextResponse(IEnumerable<string> Modules);
}
