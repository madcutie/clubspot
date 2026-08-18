using ClubSpot.SharedKernel.Primitives;

namespace ClubSpot.Application.Bookings;

public sealed record CheckoutRequest(Guid BookingId, string ClubSlug, string Title, Money Amount,
    DateTimeOffset ExpiresAt, string ReturnUrl);

public sealed record CheckoutSession(string Url);

// Port for the online payment provider. Implementations: Mercado Pago and a
// development fake. None configured means the portal only offers pay-at-club.
public interface IPaymentGateway
{
    string Name { get; }
    Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken);
    // Reconciliation (J2): every payment the provider holds for this booking, webhook or not.
    Task<IReadOnlyList<PaymentNotification>> FindPaymentsAsync(Guid bookingId, CancellationToken cancellationToken);
}
