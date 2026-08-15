using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubSpot.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClubSpotTenancy(this IServiceCollection services)
    {
        services.AddSingleton<AsyncLocalTenantContext>();
        services.AddSingleton<ITenantContext>(sp => sp.GetRequiredService<AsyncLocalTenantContext>());
        services.AddSingleton<ITenantScopeFactory>(sp => sp.GetRequiredService<AsyncLocalTenantContext>());
        return services;
    }

    public static IServiceCollection AddClubSpotPersistence(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<CoreDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(CoreDbContext.MigrationsHistoryTable, CoreDbContext.Schema)));
        return services;
    }
}
