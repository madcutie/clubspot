using ClubSpot.SharedKernel.Primitives;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Domain.Bookings;

// Settled money is append-only: it is never edited, only reversed by a counter-entry. A payment
// that has not settled yet is not money — it is the provider saying "I have seen this, I do not
// know how it ends", and that row advances in place until it does. Provisionally lives in bookings
// until the finance granularity is defined (ADR-0012).
public sealed class Payment : ITenantOwned
{
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Guid BookingId { get; private set; }
    public string Provider { get; private set; }
    // Which of the provider's channels settled it: hosted checkout today, in-person orders later.
    public PaymentRail Rail { get; private set; }
    public string ExternalId { get; private set; }
    public Money Amount { get; private set; }
    public PaymentKind Kind { get; private set; }
    public PaymentStatus Status { get; private set; }
    // Why the club is holding money it did not agree to; null while the payment is not orphaned.
    public PaymentOrphanReason? OrphanReason { get; private set; }
    // How the payment reached us: the provider's webhook or the reconciliation job (J2).
    public PaymentSource Source { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public Payment(Guid id, TenantId tenantId, Guid bookingId, string provider, PaymentRail rail,
        string externalId, Money amount, PaymentKind kind, PaymentStatus status, PaymentSource source,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider cannot be empty.", nameof(provider));
        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("External id cannot be empty.", nameof(externalId));

        Id = id;
        TenantId = tenantId;
        BookingId = bookingId;
        Provider = provider;
        Rail = rail;
        ExternalId = externalId;
        Amount = amount;
        Kind = kind;
        Status = status;
        Source = source;
        CreatedAt = createdAt;
    }

    // Which notifications still say something new about a payment already on record. Answering
    // "the row exists, therefore this is a replay" is what let a first `pending` notification burn
    // the (provider, externalId) key and swallow the approval that came after it. A payment that
    // has not settled can still land anywhere; a rejected one can still turn into money, because
    // the provider — not this row — is the authority on what it holds. Settled money is final.
    public bool Accepts(PaymentOutcome outcome) => Status switch
    {
        PaymentStatus.Pending => true,
        PaymentStatus.Rejected => outcome == PaymentOutcome.Approved,
        _ => false
    };

    // The provider reported a new state for a payment that had not settled yet: this row advances
    // instead of a second one being written, so (provider, externalId) stays the idempotency anchor.
    public void Settle(Money amount, PaymentKind kind, PaymentStatus status, PaymentSource source)
    {
        if (status == PaymentStatus.Pending)
            throw new ArgumentException("Settling a payment must reach a decided state.", nameof(status));

        Amount = amount;
        Kind = kind;
        Status = status;
        OrphanReason = null;
        Source = source;
    }

    // Money the club did not agree to; needs manual follow-up, and the reason is what the person
    // deciding reads first — so it is a column here, not only a line in the activity log.
    public void MarkOrphaned(PaymentOrphanReason reason)
    {
        Status = PaymentStatus.ApprovedOrphan;
        OrphanReason = reason;
    }

    private Payment()
    {
        Provider = null!;
        ExternalId = null!;
    }
}

public enum PaymentRail
{
    Checkout,
    Order
}

public enum PaymentKind
{
    Full,
    Deposit,
    // What a booking still owed after a deposit, whatever channel settles it.
    Balance
}

public enum PaymentStatus
{
    // The provider has the payment but has not decided it yet. Not money: it is excluded from every
    // sum of what a booking has paid, and it keeps the booking a reconciliation candidate.
    Pending,
    Approved,
    Rejected,
    ApprovedOrphan
}

// What a provider reports about a payment. Distinct from PaymentStatus because "not approved" and
// "not approved yet" are different facts, and collapsing them loses money.
public enum PaymentOutcome
{
    Pending,
    Approved,
    Rejected
}

public enum PaymentSource
{
    Webhook,
    Reconciliation
}

public enum PaymentOrphanReason
{
    // Paid on top of an already settled balance.
    Duplicate,
    // The booking was cancelled while the buyer was paying.
    BookingLost,
    // Settled in a currency the club does not charge in.
    WrongCurrency,
    // Less than the deposit or the price the slot required.
    Short,
    // The hold had expired and someone else took the slot.
    SlotLost
}
