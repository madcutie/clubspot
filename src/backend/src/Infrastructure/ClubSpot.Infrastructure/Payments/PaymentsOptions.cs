namespace ClubSpot.Infrastructure.Payments;

public sealed class PaymentsOptions
{
    public const string SectionName = "Payments";

    // none | fake | mercadopago — with none, the portal only offers pay-at-club.
    public string Provider { get; set; } = "none";
    public int HoldMinutes { get; set; } = 5;
    // Where the API itself is reachable; the fake checkout page lives there.
    public string ApiBaseUrl { get; set; } = "http://localhost:5037";
    // Public HTTPS base the gateway can call back (tunnel in dev); required for Mercado Pago.
    public string? PublicBaseUrl { get; set; }
    // Where /api/payments/return may bounce the buyer to; blocks open redirects.
    public string[] AllowedReturnOrigins { get; set; } = [];
    // The customer's receipt lives in the portal: counter checkouts send them back there.
    public string PortalBaseUrl { get; set; } = "http://localhost:5183";
}
