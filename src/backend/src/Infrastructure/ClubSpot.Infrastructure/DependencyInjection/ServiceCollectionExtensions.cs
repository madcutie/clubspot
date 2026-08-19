using ClubSpot.Application.Bookings;
using ClubSpot.Application.Core;
using ClubSpot.Application.Core.Users;
using ClubSpot.Application.Core.People;
using ClubSpot.Infrastructure.Auth;
using ClubSpot.Infrastructure.Modularity;
using ClubSpot.Infrastructure.Payments;
using ClubSpot.Infrastructure.Persistence;
using ClubSpot.Infrastructure.Repositories;
using ClubSpot.SharedKernel.Modularity;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        services.AddDbContext<ClubSpotDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IClubSettings, ClubSettings>();
        services.AddScoped<IClubDirectory, ClubDirectory>();
        return services;
    }

    // Registers the options and the dev fake only. Real vendors live in their own projects
    // (e.g. ClubSpot.Infrastructure.MercadoPago) and are wired by the host.
    public static IServiceCollection AddClubSpotPayments(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PaymentsOptions>(configuration.GetSection(PaymentsOptions.SectionName));
        if (configuration[$"{PaymentsOptions.SectionName}:Provider"] == FakePaymentProvider.ProviderName)
        {
            services.AddScoped<FakePaymentProvider>();
            services.AddScoped<IPaymentProvider>(services => services.GetRequiredService<FakePaymentProvider>());
            services.AddScoped<IHostedCheckout>(services => services.GetRequiredService<FakePaymentProvider>());
        }
        return services;
    }

    public static IServiceCollection AddClubSpotAuth(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        return services;
    }

    public static IServiceCollection AddClubSpotPeople(this IServiceCollection services)
    {
        services.AddScoped<IPersonRepository, PersonRepository>();
        services.AddScoped<IPeopleQueries, PeopleQueries>();
        services.AddScoped<IPeopleLink, PeopleLink>();
        services.AddScoped<CreatePersonHandler>();
        services.AddScoped<BlockPeopleHandler>();
        services.AddScoped<AddNoteHandler>();
        services.AddScoped<RegisterPersonPaymentHandler>();
        return services;
    }

    public static IServiceCollection AddClubSpotBookings(this IServiceCollection services)
    {
        services.AddScoped<ISchedulesStore, SchedulesStore>();
        services.AddScoped<ICourtsStore, CourtsStore>();
        services.AddScoped<IAvailabilityOverridesStore, AvailabilityOverridesStore>();
        services.AddScoped<IAvailabilityQueries, AvailabilityQueries>();
        services.AddScoped<IBookingsStore, BookingsStore>();
        services.AddScoped<ReconcileOnlinePaymentsHandler>();
        services.AddScoped<SettleBookingHandler>();
        services.AddScoped<GetSchedulesHandler>();
        services.AddScoped<ReplaceSchedulesHandler>();
        services.AddScoped<GetCourtsHandler>();
        services.AddScoped<ReplaceCourtsHandler>();
        services.AddScoped<GetAgendaHandler>();
        services.AddScoped<GetPortalCatalogHandler>();
        services.AddScoped<GetPortalAvailabilityHandler>();
        return services;
    }

    public static IServiceCollection AddClubSpotModularity(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddScoped<ITenantModules, TenantModulesProvider>();
        return services;
    }
}
