using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ClubSpot.Infrastructure.MercadoPago;

// Validates the x-signature header: HMAC-SHA256 over the manifest
// "id:[data.id];request-id:[x-request-id];ts:[ts];" — sections without a value are omitted,
// and an alphanumeric data.id goes lowercase, as the Mercado Pago spec mandates.
public static class MercadoPagoWebhookSignature
{
    // A signature stays valid forever unless its timestamp is checked, so a captured notification could
    // be replayed for as long as the secret lives. The payment is refetched and the payment ledger is
    // idempotent, so a replay cannot move money — it can only make the API work for free.
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(15);

    public static bool IsValid(string secret, string? xSignature, string? xRequestId, string? dataId,
        DateTimeOffset now)
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
        if (!IsFresh(ts, now)) return false;

        var manifest = new StringBuilder();
        if (!string.IsNullOrEmpty(dataId)) manifest.Append($"id:{dataId.ToLowerInvariant()};");
        if (!string.IsNullOrEmpty(xRequestId)) manifest.Append($"request-id:{xRequestId};");
        manifest.Append($"ts:{ts};");

        var computed = Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest.ToString())));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed), Encoding.UTF8.GetBytes(hash.ToLowerInvariant()));
    }

    // Mercado Pago documents ts as a Unix timestamp and has been seen sending milliseconds; both are
    // read. A ts that is not a number at all fails the signature on its own, so freshness lets it pass
    // rather than inventing a second reason to reject — never the other way round.
    private static bool IsFresh(string ts, DateTimeOffset now)
    {
        if (!long.TryParse(ts, CultureInfo.InvariantCulture, out var value) || value <= 0) return true;
        if (value >= 100_000_000_000_000L) return false;

        var stamp = value >= 100_000_000_000L
            ? DateTimeOffset.FromUnixTimeMilliseconds(value)
            : DateTimeOffset.FromUnixTimeSeconds(value);
        return (now - stamp).Duration() <= MaxAge;
    }
}
