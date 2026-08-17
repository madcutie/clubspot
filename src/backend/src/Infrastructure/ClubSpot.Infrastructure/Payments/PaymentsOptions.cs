namespace ClubSpot.Infrastructure.Payments;

public sealed class PaymentsOptions
{
    public const string SectionName = "Payments";

    // none | fake | mercadopago — with none, the portal only offers pay-at-club.
    public string Gateway { get; set; } = "none";
    public int HoldMinutes { get; set; } = 15;
    // Where the API itself is reachable; the fake checkout page lives there.
    public string ApiBaseUrl { get; set; } = "http://localhost:5037";
    // Public HTTPS base the gateway can call back (tunnel in dev); required for Mercado Pago.
    public string? PublicBaseUrl { get; set; }
}
