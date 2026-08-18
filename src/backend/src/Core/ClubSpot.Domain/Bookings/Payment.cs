using ClubSpot.SharedKernel.Primitives;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Domain.Bookings;

// Append-only: a payment is never edited. Provisionally lives in bookings until the
// finance granularity is defined (ADR-0012).
public sealed class Payment : ITenantOwned
{
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Guid BookingId { get; private set; }
    public string Gateway { get; private set; }
    public string ExternalId { get; private set; }
    public Money Amount { get; private set; }
    public PaymentKind Kind { get; private set; }
    public PaymentStatus Status { get; private set; }
    // How the payment reached us: the provider's webhook or the reconciliation job (J2).
    public PaymentSource Source { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public Payment(Guid id, TenantId tenantId, Guid bookingId, string gateway, string externalId,
        Money amount, PaymentKind kind, PaymentStatus status, PaymentSource source, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(gateway))
            throw new ArgumentException("Gateway cannot be empty.", nameof(gateway));
        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("External id cannot be empty.", nameof(externalId));

        Id = id;
        TenantId = tenantId;
        BookingId = bookingId;
        Gateway = gateway;
        ExternalId = externalId;
        Amount = amount;
        Kind = kind;
        Status = status;
        Source = source;
        CreatedAt = createdAt;
    }

    // The slot was gone by the time the approved payment arrived; needs manual follow-up.
    public void MarkOrphaned() => Status = PaymentStatus.ApprovedOrphan;

    private Payment()
    {
        Gateway = null!;
        ExternalId = null!;
    }
}

public enum PaymentKind
{
    Full,
    Deposit
}

public enum PaymentStatus
{
    Approved,
    Rejected,
    ApprovedOrphan
}

public enum PaymentSource
{
    Webhook,
    Reconciliation
}
