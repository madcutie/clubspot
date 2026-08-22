using ClubSpot.SharedKernel.Primitives;
using ClubSpot.Application.Bookings;
using ClubSpot.Domain.Bookings;

namespace ClubSpot.UnitTests.Bookings;

public sealed class SettleBookingHandlerTests
{
    private static readonly Guid Booking = Guid.NewGuid();

    [Fact]
    public async Task Applies_the_payment_the_provider_holds()
    {
        var store = new StoreFake();
        var provider = new ProviderFake(
            [new PaymentNotification(Booking, "mercadopago", PaymentRail.Checkout, "111", PaymentOutcome.Approved, 12000)]);
        var handler = new SettleBookingHandler(store, [provider]);

        var outcome = await handler.HandleAsync(Booking, CancellationToken.None);

        Assert.Equal(PaymentApplyOutcome.Confirmed, outcome);
        Assert.Equal("111", Assert.Single(store.AppliedNotifications).ExternalId);
    }

    [Fact]
    public async Task Returns_null_when_no_provider_holds_a_payment()
    {
        var handler = new SettleBookingHandler(new StoreFake(), [new ProviderFake([])]);

        Assert.Null(await handler.HandleAsync(Booking, CancellationToken.None));
    }

    [Fact]
    public async Task A_rejected_attempt_is_recorded_and_the_search_continues()
    {
        var store = new StoreFake();
        var provider = new ProviderFake(
        [
            new PaymentNotification(Booking, "mercadopago", PaymentRail.Checkout, "222", PaymentOutcome.Rejected, 12000),
            new PaymentNotification(Booking, "mercadopago", PaymentRail.Checkout, "333", PaymentOutcome.Approved, 12000)
        ]);
        var handler = new SettleBookingHandler(store, [provider]);

        var outcome = await handler.HandleAsync(Booking, CancellationToken.None);

        Assert.Equal(PaymentApplyOutcome.Confirmed, outcome);
        Assert.Equal(2, store.AppliedNotifications.Count);
    }

    private sealed class StoreFake : IBookingsStore
    {
        public List<PaymentNotification> AppliedNotifications { get; } = [];

        public Task<PaymentApplyOutcome> ApplyPaymentAsync(PaymentNotification notification, PaymentSource source, CancellationToken cancellationToken)
        {
            Assert.Equal(PaymentSource.Reconciliation, source);
            AppliedNotifications.Add(notification);
            return Task.FromResult(notification.Outcome switch
            {
                PaymentOutcome.Approved => PaymentApplyOutcome.Confirmed,
                PaymentOutcome.Pending => PaymentApplyOutcome.Pending,
                _ => PaymentApplyOutcome.Rejected
            });
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

        public Task<IReadOnlyList<Guid>> GetUnsettledOnlineBookingIdsAsync(
            DateTimeOffset since, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ProviderFake(IReadOnlyList<PaymentNotification> payments) : IPaymentProvider
    {
        public string Name => "fake";

        public Task<IReadOnlyList<PaymentNotification>> FindPaymentsAsync(Guid bookingId, CancellationToken cancellationToken) =>
            Task.FromResult(payments);
    }
}
