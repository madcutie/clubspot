using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Time;

namespace ClubSpot.Application.Bookings;

public sealed record ReconciliationResult(string Provider, int Candidates, int Applied, int Orphaned);

// J2: for every online booking still unpaid on our side, ask each registered provider whether
// it holds a payment we missed (lost webhook) and apply it through the same idempotent path
// the webhook uses. Runs per tenant; the caller opens the tenant scope.
public sealed class ReconcileOnlinePaymentsHandler(
    IBookingsStore store, IEnumerable<IPaymentProvider> providers, IClock clock)
{
    private static readonly TimeSpan Lookback = TimeSpan.FromHours(48);
    private const int BatchLimit = 200;

    public async Task<IReadOnlyList<ReconciliationResult>> HandleAsync(CancellationToken cancellationToken)
    {
        var candidates = await store.GetUnsettledOnlineBookingIdsAsync(
            clock.UtcNow - Lookback, BatchLimit, cancellationToken);

        var results = new List<ReconciliationResult>();
        foreach (var provider in providers)
        {
            var applied = 0;
            var orphaned = 0;
            foreach (var bookingId in candidates)
            {
                foreach (var payment in await provider.FindPaymentsAsync(bookingId, cancellationToken))
                {
                    var outcome = await store.ApplyPaymentAsync(payment, PaymentSource.Reconciliation, cancellationToken);
                    if (outcome == PaymentApplyOutcome.Confirmed) applied++;
                    else if (outcome == PaymentApplyOutcome.Orphaned) orphaned++;
                }
            }
            results.Add(new ReconciliationResult(provider.Name, candidates.Count, applied, orphaned));
        }
        return results;
    }
}
