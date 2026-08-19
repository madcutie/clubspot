using ClubSpot.SharedKernel.Primitives;

namespace ClubSpot.Application.Bookings;

public sealed record CheckoutRequest(Guid BookingId, string ClubSlug, string Title, Money Amount,
    DateTimeOffset ExpiresAt, string ReturnUrl);

public sealed record CheckoutSession(string Url);

// Port for a payment provider (ADR-0014). Identity and reconciliation are common to every
// provider; each way of charging is an optional capability the provider implements or not.
// A provider without a capability means that way of charging is not offered — never an error.
public interface IPaymentProvider
{
    string Name { get; }
    // Reconciliation (J2): every payment the provider holds for this booking, webhook or not.
    Task<IReadOnlyList<PaymentNotification>> FindPaymentsAsync(Guid bookingId, CancellationToken cancellationToken);
}

// Capability: online checkout hosted by the provider, reached by redirect.
public interface IHostedCheckout : IPaymentProvider
{
    Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken);
}
