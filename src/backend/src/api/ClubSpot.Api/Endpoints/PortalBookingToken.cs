using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClubSpot.SharedKernel.Time;
using ClubSpot.Api.Auth;
using Microsoft.Extensions.Options;

namespace ClubSpot.Api.Endpoints;

// Proof that the caller is the one who made the booking. The portal has no login, and the booking id
// is not a secret — it travels to Mercado Pago as external_reference and lands in the buyer's address
// bar — so knowing an id must not be enough to read, release or settle a booking.
//
// Derived instead of stored: nothing to migrate and nothing to leak from the bookings table. What it
// signs is the id *and* the moment it was handed out, so a token that escapes stops working on its
// own — see MaxAge.
public sealed class PortalBookingToken(IOptions<JwtOptions> jwt, IClock clock)
{
    public const string HeaderName = "X-Booking-Token";

    // Domain separation: the signing key is shared with the JWT issuer and these tokens must never be
    // interchangeable with an access token.
    private static readonly byte[] Label = Encoding.UTF8.GetBytes("clubspot:portal-booking:");

    // A booking id is forever, so a signature over the id alone is a credential that never expires
    // and cannot be revoked: once it leaks — browser history, a proxy log, a screenshot of the
    // receipt — it works for good. The issue time is signed with it and bounds that.
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    private readonly byte[] _key = Encoding.UTF8.GetBytes(jwt.Value.SigningKey);

    public string For(Guid bookingId) => For(bookingId, clock.UtcNow.ToUnixTimeSeconds());

    public bool IsValid(Guid bookingId, string? token)
    {
        if (string.IsNullOrEmpty(token)) return false;

        var separator = token.IndexOf('.');
        if (separator <= 0) return false;
        if (!long.TryParse(token[..separator], CultureInfo.InvariantCulture, out var issuedAt)) return false;
        // Bounds before conversion: FromUnixTimeSeconds throws outside them, and this is an anonymous
        // endpoint, so a crafted stamp would answer 500 instead of the 404 everything else answers.
        if (issuedAt < MinIssuedAt || issuedAt > MaxIssuedAt) return false;

        // Age next, but never as the only gate: an unsigned token with a fresh stamp must still fail.
        var age = clock.UtcNow - DateTimeOffset.FromUnixTimeSeconds(issuedAt);
        if (age < -Skew || age > MaxAge) return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(For(bookingId, issuedAt)), Encoding.UTF8.GetBytes(token));
    }

    // A token minted a moment ago on a host whose clock runs slightly ahead is still this system's.
    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(5);

    private static readonly long MinIssuedAt = DateTimeOffset.MinValue.ToUnixTimeSeconds();
    private static readonly long MaxIssuedAt = DateTimeOffset.MaxValue.ToUnixTimeSeconds();

    private string For(Guid bookingId, long issuedAt)
    {
        byte[] message = [.. Label, .. bookingId.ToByteArray(), .. BitConverter.GetBytes(issuedAt)];
        return $"{issuedAt.ToString(CultureInfo.InvariantCulture)}.{Convert.ToHexStringLower(HMACSHA256.HashData(_key, message))}";
    }
}
