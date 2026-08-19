using System.Security.Cryptography;
using System.Text;
using ClubSpot.Api.Auth;
using Microsoft.Extensions.Options;

namespace ClubSpot.Api.Endpoints;

// Proof that the caller is the one who made the booking. The portal has no login, and the booking id
// is not a secret — it travels to Mercado Pago as external_reference and lands in the buyer's address
// bar — so knowing an id must not be enough to read, release or settle a booking.
//
// Derived from the id instead of stored: nothing to migrate, nothing to leak from the bookings table,
// and no revocation needed because the booking's own lifecycle already ends it.
public sealed class PortalBookingToken(IOptions<JwtOptions> jwt)
{
    public const string HeaderName = "X-Booking-Token";

    // Domain separation: the signing key is shared with the JWT issuer and these tokens must never be
    // interchangeable with an access token.
    private static readonly byte[] Label = Encoding.UTF8.GetBytes("clubspot:portal-booking:");

    private readonly byte[] _key = Encoding.UTF8.GetBytes(jwt.Value.SigningKey);

    public string For(Guid bookingId)
    {
        byte[] message = [.. Label, .. bookingId.ToByteArray()];
        return Convert.ToHexStringLower(HMACSHA256.HashData(_key, message));
    }

    public bool IsValid(Guid bookingId, string? token) =>
        !string.IsNullOrEmpty(token)
        && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(For(bookingId)), Encoding.UTF8.GetBytes(token));
}
