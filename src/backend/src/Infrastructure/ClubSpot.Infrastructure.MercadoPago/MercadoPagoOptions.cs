namespace ClubSpot.Infrastructure.MercadoPago;

public sealed class MercadoPagoOptions
{
    public const string SectionName = "Payments:MercadoPago";

    // Secrets: user-secrets or environment variables, never a committed appsettings.
    public string AccessToken { get; set; } = "";
    // Panel key that signs x-signature; without it the webhook rejects everything.
    public string WebhookSecret { get; set; } = "";
    // Sandbox quirk: payments made with test credentials are signed by the test seller's shadow
    // application, whose secret the panel never shows — so dev turns this off. Processing an
    // unsigned notification is still safe here: the payload is never trusted, the payment is
    // always fetched back from Mercado Pago's API. Production keeps it on.
    public bool RequireValidSignature { get; set; } = true;
    // Copied from Payments:PublicBaseUrl at registration; Mercado Pago calls back here.
    public string PublicBaseUrl { get; set; } = "";
}
