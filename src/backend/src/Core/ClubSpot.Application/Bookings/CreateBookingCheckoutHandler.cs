using ClubSpot.Application.Core;
using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Primitives;
using ClubSpot.SharedKernel.Time;

namespace ClubSpot.Application.Bookings;

public enum BookingCheckoutOutcome { Created, NotFound, NotChargeable, NoProvider }

public sealed record BookingCheckoutResult(BookingCheckoutOutcome Outcome, string? Url = null,
    decimal Amount = 0, DateTimeOffset? ExpiresAt = null);

// Counter charge of an already confirmed booking: the slot is the customer's before paying, so
// there is no hold to protect and the link can be reissued as often as the operator needs
// (plan de cobro en mostrador, 19/08/2026). Charges the outstanding balance, never the price.
public sealed class CreateBookingCheckoutHandler(
    IBookingsStore store, IEnumerable<IHostedCheckout> checkouts, IClubSettings clubSettings, IClock clock)
{
    private static readonly TimeSpan MinimumLifetime = TimeSpan.FromHours(1);

    public async Task<BookingCheckoutResult> HandleAsync(Guid bookingId, string returnUrl,
        CancellationToken cancellationToken)
    {
        var checkout = checkouts.FirstOrDefault();
        if (checkout is null) return new BookingCheckoutResult(BookingCheckoutOutcome.NoProvider);

        var booking = await store.GetAsync(bookingId, cancellationToken);
        if (booking is null) return new BookingCheckoutResult(BookingCheckoutOutcome.NotFound);

        var due = booking.Price - booking.PaidAmount;
        if (booking.Status != BookingStatus.Confirmed || due <= 0)
            return new BookingCheckoutResult(BookingCheckoutOutcome.NotChargeable);

        var club = await clubSettings.GetAsync(cancellationToken);
        var calendar = new ClubCalendar(TimeZoneInfo.FindSystemTimeZoneById(club.TimeZone), clock);
        // The link dies with the match, so none is left alive for a slot already played — but a
        // late charge still gets an hour, which is why the operator asked for it just now.
        var endMinute = booking.StartMinute + booking.DurationMinutes;
        var endsAt = calendar.ToUtc(booking.Date.AddDays(endMinute / 1440),
            new TimeOnly(endMinute % 1440 / 60, endMinute % 60));
        var expiresAt = endsAt > clock.UtcNow + MinimumLifetime ? endsAt : clock.UtcNow + MinimumLifetime;
        var amount = Money.Of(due, club.Currency);

        // Reissuing stays free — the slot is already the customer's — but handing out a second payable
        // link for the same money is not. Any link for this same charge that has not expired is handed
        // back instead: minting another cannot void the first, so a second one only ever adds a way to
        // pay twice. Once none is live there is nothing to protect and a fresh one is issued.
        if (await store.FindLiveCheckoutAsync(booking.Id, checkout.Name, amount, clock.UtcNow, cancellationToken)
            is { } live)
            return new BookingCheckoutResult(BookingCheckoutOutcome.Created, live.Url, due, live.ExpiresAt);

        var title = $"{club.Name} · {booking.CourtName} {booking.Date:dd/MM} {Hour(booking.StartMinute)}";
        var session = await checkout.CreateCheckoutAsync(new CheckoutRequest(booking.Id, club.Slug, title,
            amount, expiresAt, returnUrl), cancellationToken);

        var handed = await store.RecordCheckoutIssuedAsync(new CheckoutIssued(booking.Id, checkout.Name,
            session.Url, amount, expiresAt), cancellationToken);
        return new BookingCheckoutResult(BookingCheckoutOutcome.Created, handed.Url, due, handed.ExpiresAt);
    }

    private static string Hour(int minute) => $"{minute / 60:00}:{minute % 60:00}";
}
