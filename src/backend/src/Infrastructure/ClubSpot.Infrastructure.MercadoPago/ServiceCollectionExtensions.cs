using ClubSpot.Application.Bookings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClubSpot.Infrastructure.MercadoPago;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClubSpotMercadoPago(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MercadoPagoOptions>(options =>
        {
            configuration.GetSection(MercadoPagoOptions.SectionName).Bind(options);
            options.PublicBaseUrl = configuration["Payments:PublicBaseUrl"] ?? "";
            if (string.IsNullOrWhiteSpace(options.AccessToken))
                throw new InvalidOperationException("Payments:MercadoPago:AccessToken is required (local appsettings.Development.json).");
            // PublicBaseUrl is only needed to create checkouts; the gateway validates it there,
            // so a host that only reconciles (JobService) can run without it.
        });
        services.AddScoped<MercadoPagoGateway>();
        services.AddScoped<IPaymentGateway>(provider => provider.GetRequiredService<MercadoPagoGateway>());
        return services;
    }
}
