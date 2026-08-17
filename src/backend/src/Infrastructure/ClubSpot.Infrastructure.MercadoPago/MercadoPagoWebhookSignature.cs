using System.Security.Cryptography;
using System.Text;

namespace ClubSpot.Infrastructure.MercadoPago;

// Validates the x-signature header: HMAC-SHA256 over the manifest
// "id:[data.id];request-id:[x-request-id];ts:[ts];" — sections without a value are omitted,
// and an alphanumeric data.id goes lowercase, as the Mercado Pago spec mandates.
public static class MercadoPagoWebhookSignature
{
    public static bool IsValid(string secret, string? xSignature, string? xRequestId, string? dataId)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(xSignature)) return false;

        string? ts = null, hash = null;
        foreach (var part in xSignature.Split(','))
        {
            var separator = part.IndexOf('=');
            if (separator < 0) continue;
            var key = part[..separator].Trim();
            var value = part[(separator + 1)..].Trim();
            if (key == "ts") ts = value;
            else if (key == "v1") hash = value;
        }
        if (ts is null || hash is null) return false;

        var manifest = new StringBuilder();
        if (!string.IsNullOrEmpty(dataId)) manifest.Append($"id:{dataId.ToLowerInvariant()};");
        if (!string.IsNullOrEmpty(xRequestId)) manifest.Append($"request-id:{xRequestId};");
        manifest.Append($"ts:{ts};");

        var computed = Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest.ToString())));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed), Encoding.UTF8.GetBytes(hash.ToLowerInvariant()));
    }
}
