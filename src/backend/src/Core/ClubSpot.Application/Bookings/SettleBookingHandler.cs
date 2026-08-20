using ClubSpot.Domain.Bookings;

namespace ClubSpot.Application.Bookings;

// On-demand settlement of one booking: the portal calls this a few seconds after the buyer
// returns from the checkout, so a lost webhook is repaired right away instead of waiting
// for the next J2 run. Same idempotent path as the webhook and J2.
public sealed class SettleBookingHandler(IBookingsStore store, IEnumerable<IPaymentProvider> providers)
{
    public async Task<PaymentApplyOutcome?> HandleAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        foreach (var provider in providers)
            foreach (var payment in await provider.FindPaymentsAsync(bookingId, cancellationToken))
            {
                var outcome = await store.ApplyPaymentAsync(payment, PaymentSource.Reconciliation, cancellationToken);
                // A rejected attempt is recorded but the buyer may have retried, and one the provider
                // has not decided settles nothing: either way, keep looking for the one that did.
                if (outcome is not (PaymentApplyOutcome.Rejected or PaymentApplyOutcome.Pending)) return outcome;
            }
        return null;
    }
}
