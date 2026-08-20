using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Primitives;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.UnitTests.Bookings;

public sealed class PaymentTests
{
    [Theory]
    [InlineData(PaymentOutcome.Approved)]
    [InlineData(PaymentOutcome.Rejected)]
    [InlineData(PaymentOutcome.Pending)]
    public void A_payment_the_provider_has_not_decided_accepts_any_later_word(PaymentOutcome outcome) =>
        Assert.True(Existing(PaymentStatus.Pending).Accepts(outcome));

    [Fact]
    public void A_rejected_payment_still_accepts_becoming_money() =>
        Assert.True(Existing(PaymentStatus.Rejected).Accepts(PaymentOutcome.Approved));

    [Theory]
    [InlineData(PaymentOutcome.Rejected)]
    [InlineData(PaymentOutcome.Pending)]
    public void A_rejected_payment_does_not_walk_back(PaymentOutcome outcome) =>
        Assert.False(Existing(PaymentStatus.Rejected).Accepts(outcome));

    [Theory]
    [InlineData(PaymentStatus.Approved)]
    [InlineData(PaymentStatus.ApprovedOrphan)]
    public void Money_already_booked_is_final(PaymentStatus status)
    {
        var payment = Existing(status);
        Assert.False(payment.Accepts(PaymentOutcome.Approved));
        Assert.False(payment.Accepts(PaymentOutcome.Rejected));
        Assert.False(payment.Accepts(PaymentOutcome.Pending));
    }

    [Fact]
    public void Settling_records_what_the_provider_finally_reported()
    {
        var payment = Existing(PaymentStatus.Pending);

        payment.Settle(Money.Of(9000m, "ARS"), PaymentKind.Balance, PaymentStatus.Approved,
            PaymentSource.Reconciliation);

        Assert.Equal(PaymentStatus.Approved, payment.Status);
        Assert.Equal(9000m, payment.Amount.Amount);
        Assert.Equal(PaymentKind.Balance, payment.Kind);
        Assert.Equal(PaymentSource.Reconciliation, payment.Source);
        Assert.Null(payment.OrphanReason);
    }

    [Fact]
    public void Settling_has_to_reach_a_decided_state() =>
        Assert.Throws<ArgumentException>(() => Existing(PaymentStatus.Pending)
            .Settle(Money.Of(1m, "ARS"), PaymentKind.Full, PaymentStatus.Pending, PaymentSource.Webhook));

    private static Payment Existing(PaymentStatus status) => new(
        Guid.NewGuid(), TenantId.From(Guid.NewGuid()), Guid.NewGuid(), "mercadopago", PaymentRail.Checkout,
        "external-1", Money.Of(12000m, "ARS"), PaymentKind.Full, status, PaymentSource.Webhook,
        DateTimeOffset.UtcNow);
}
