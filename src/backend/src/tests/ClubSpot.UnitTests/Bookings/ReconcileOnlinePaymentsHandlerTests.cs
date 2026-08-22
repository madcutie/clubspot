using ClubSpot.SharedKernel.Primitives;
using ClubSpot.Application.Bookings;
using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Time;

namespace ClubSpot.UnitTests.Bookings;

public sealed class ReconcileOnlinePaymentsHandlerTests
{
    private static readonly Guid Unpaid = Guid.NewGuid();
    private static readonly Guid Abandoned = Guid.NewGuid();

    [Fact]
    public async Task Applies_the_payments_the_provider_holds_for_unsettled_bookings()
    {
        var store = new StoreFake([Unpaid, Abandoned]);
        var provider = new ProviderFake(new Dictionary<Guid, PaymentNotification[]>
        {
            [Unpaid] = [new PaymentNotification(Unpaid, "mercadopago", PaymentRail.Checkout, "111", PaymentOutcome.Approved, 12000)],
            [Abandoned] = []
        });
        var handler = new ReconcileOnlinePaymentsHandler(store, [provider], new ClockFake(DateTimeOffset.UtcNow));

        var results = await handler.HandleAsync(CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal(new ReconciliationResult("fake", Candidates: 2, Applied: 1, Orphaned: 0), result);
        var applied = Assert.Single(store.AppliedNotifications);
        Assert.Equal("111", applied.ExternalId);
    }

    [Fact]
    public async Task Every_registered_provider_reconciles_and_reports_on_its_own()
    {
        var store = new StoreFake([Unpaid]);
        var empty = new ProviderFake(new Dictionary<Guid, PaymentNotification[]>(), name: "one");
        var holding = new ProviderFake(new Dictionary<Guid, PaymentNotification[]>
        {
            [Unpaid] = [new PaymentNotification(Unpaid, "two", PaymentRail.Checkout, "333", PaymentOutcome.Approved, 12000)]
        }, name: "two");
        var handler = new ReconcileOnlinePaymentsHandler(store, [empty, holding], new ClockFake(DateTimeOffset.UtcNow));

        var results = await handler.HandleAsync(CancellationToken.None);

        Assert.Equal(
            [new ReconciliationResult("one", 1, 0, 0), new ReconciliationResult("two", 1, 1, 0)],
            results);
    }

    [Fact]
    public async Task A_payment_whose_slot_was_resold_counts_as_orphaned()
    {
        var store = new StoreFake([Unpaid]) { OutcomeToReturn = PaymentApplyOutcome.Orphaned };
        var provider = new ProviderFake(new Dictionary<Guid, PaymentNotification[]>
        {
            [Unpaid] = [new PaymentNotification(Unpaid, "mercadopago", PaymentRail.Checkout, "222", PaymentOutcome.Approved, 12000)]
        });
        var handler = new ReconcileOnlinePaymentsHandler(store, [provider], new ClockFake(DateTimeOffset.UtcNow));

        var results = await handler.HandleAsync(CancellationToken.None);

        Assert.Equal(new ReconciliationResult("fake", Candidates: 1, Applied: 0, Orphaned: 1), Assert.Single(results));
    }

    [Fact]
    public async Task The_batch_is_bounded_by_a_lookback_window()
    {
        var now = new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);
        var store = new StoreFake([]);
        var handler = new ReconcileOnlinePaymentsHandler(
            store, [new ProviderFake(new Dictionary<Guid, PaymentNotification[]>())], new ClockFake(now));

        await handler.HandleAsync(CancellationToken.None);

        Assert.Equal(now.AddHours(-48), store.RequestedSince);
        Assert.True(store.RequestedLimit > 0);
    }

    private sealed class StoreFake(IReadOnlyList<Guid> candidates) : IBookingsStore
    {
        public PaymentApplyOutcome OutcomeToReturn { get; init; } = PaymentApplyOutcome.Confirmed;
        public List<PaymentNotification> AppliedNotifications { get; } = [];
        public DateTimeOffset RequestedSince { get; private set; }
        public int RequestedLimit { get; private set; }

        public Task<IReadOnlyList<Guid>> GetUnsettledOnlineBookingIdsAsync(
            DateTimeOffset since, int limit, CancellationToken cancellationToken)
        {
            RequestedSince = since;
            RequestedLimit = limit;
            return Task.FromResult(candidates);
        }

        public Task<PaymentApplyOutcome> ApplyPaymentAsync(PaymentNotification notification, PaymentSource source, CancellationToken cancellationToken)
        {
            Assert.Equal(PaymentSource.Reconciliation, source);
            AppliedNotifications.Add(notification);
            return Task.FromResult(OutcomeToReturn);
        }

        public Task<BookingCreateResult> CreateAsync(BookingCreateInput input, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BookingCancelOutcome> CancelAsync(Guid id, string reason, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<HoldReleaseOutcome> ReleaseHoldAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CheckoutIssued> RecordCheckoutIssuedAsync(CheckoutIssued issued, CancellationToken cancellationToken) =>
            Task.FromResult(issued);

        public Task<CheckoutIssued?> FindLiveCheckoutAsync(Guid bookingId, string provider, Money amount,
            DateTimeOffset asOf, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BookingSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ProviderFake(
        IReadOnlyDictionary<Guid, PaymentNotification[]> payments, string name = "fake") : IPaymentProvider
    {
        public string Name => name;

        public Task<IReadOnlyList<PaymentNotification>> FindPaymentsAsync(Guid bookingId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PaymentNotification>>(
                payments.TryGetValue(bookingId, out var found) ? found : []);
    }

    private sealed class ClockFake(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
