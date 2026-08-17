namespace ClubSpot.Infrastructure.MercadoPago;

public sealed class MercadoPagoOptions
{
    public const string SectionName = "Payments:MercadoPago";

    // Secrets: user-secrets or environment variables, never a committed appsettings.
    public string AccessToken { get; set; } = "";
    // Panel key that signs x-signature; without it the webhook rejects everything.
    public string WebhookSecret { get; set; } = "";
    // Copied from Payments:PublicBaseUrl at registration; Mercado Pago calls back here.
    public string PublicBaseUrl { get; set; } = "";
}
