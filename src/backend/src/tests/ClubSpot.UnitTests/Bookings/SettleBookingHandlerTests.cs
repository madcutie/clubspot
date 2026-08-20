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
            [new PaymentNotification(Booking, "mercadopago", PaymentRail.Checkout, "111", Approved: true, 12000)]);
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
            new PaymentNotification(Booking, "mercadopago", PaymentRail.Checkout, "222", Approved: false, 12000),
            new PaymentNotification(Booking, "mercadopago", PaymentRail.Checkout, "333", Approved: true, 12000)
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
            return Task.FromResult(notification.Approved ? PaymentApplyOutcome.Confirmed : PaymentApplyOutcome.Rejected);
        }

        public Task<BookingCreateResult> CreateAsync(BookingCreateInput input, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BookingCancelOutcome> CancelAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<HoldReleaseOutcome> ReleaseHoldAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RecordCheckoutIssuedAsync(Guid bookingId, Money amount, DateTimeOffset expiresAt,
            string provider, CancellationToken cancellationToken) => Task.CompletedTask;

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
