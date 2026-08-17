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
                throw new InvalidOperationException("Payments:MercadoPago:AccessToken is required (user-secrets in dev).");
            if (string.IsNullOrWhiteSpace(options.WebhookSecret))
                throw new InvalidOperationException("Payments:MercadoPago:WebhookSecret is required (user-secrets in dev).");
            if (string.IsNullOrWhiteSpace(options.PublicBaseUrl))
                throw new InvalidOperationException("Payments:PublicBaseUrl is required for Mercado Pago.");
        });
        services.AddScoped<MercadoPagoGateway>();
        services.AddScoped<IPaymentGateway>(provider => provider.GetRequiredService<MercadoPagoGateway>());
        return services;
    }
}
