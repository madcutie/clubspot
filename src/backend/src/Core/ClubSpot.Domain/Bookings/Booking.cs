using ClubSpot.SharedKernel.Primitives;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Domain.Bookings;

public sealed class Booking : ITenantOwned
{
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Guid CourtId { get; private set; }
    public DateOnly Date { get; private set; }
    public int StartMinute { get; private set; }
    public int DurationMinutes { get; private set; }
    public Money Price { get; private set; }
    // Contact as typed for this booking; the durable identity is PersonId (ADR-0012).
    public string CustomerName { get; private set; }
    public string? CustomerPhone { get; private set; }
    // Null only for counter bookings: the backoffice panel does not resolve a person yet.
    public Guid? PersonId { get; private set; }
    public BookingStatus Status { get; private set; }
    public BookingOrigin Origin { get; private set; }
    public PaymentMode PaymentMode { get; private set; }
    // Only for online-payment holds: past this instant the hold no longer blocks the slot.
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    // Null for portal bookings: the customer books without an operator.
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    // Business datum, not a log line: the counter reads it off the booking, never off the activity log.
    public string? CancellationReason { get; private set; }

    public const int CancellationReasonMaxLength = 300;

    public Booking(Guid id, TenantId tenantId, Guid courtId, DateOnly date, int startMinute, int durationMinutes,
        Money price, string customerName, string? customerPhone, Guid? personId, BookingOrigin origin,
        DateTimeOffset createdAt, Guid? createdBy)
        : this(id, tenantId, courtId, date, startMinute, durationMinutes, price, customerName, customerPhone,
            personId, origin, PaymentMode.Club, BookingStatus.Confirmed, expiresAt: null, createdAt, createdBy)
    {
    }

    public static Booking Hold(Guid id, TenantId tenantId, Guid courtId, DateOnly date, int startMinute,
        int durationMinutes, Money price, string customerName, string? customerPhone, Guid? personId,
        BookingOrigin origin, PaymentMode paymentMode, DateTimeOffset expiresAt, DateTimeOffset createdAt,
        Guid? createdBy)
    {
        if (paymentMode == PaymentMode.Club)
            throw new ArgumentException("A club-paid booking confirms immediately; it never holds.", nameof(paymentMode));
        if (expiresAt <= createdAt)
            throw new ArgumentException("A hold must expire after its creation.", nameof(expiresAt));

        return new Booking(id, tenantId, courtId, date, startMinute, durationMinutes, price, customerName,
            customerPhone, personId, origin, paymentMode, BookingStatus.PendingPayment, expiresAt, createdAt, createdBy);
    }

    private Booking(Guid id, TenantId tenantId, Guid courtId, DateOnly date, int startMinute, int durationMinutes,
        Money price, string customerName, string? customerPhone, Guid? personId, BookingOrigin origin,
        PaymentMode paymentMode, BookingStatus status, DateTimeOffset? expiresAt, DateTimeOffset createdAt,
        Guid? createdBy)
    {
        if (durationMinutes <= 0)
            throw new ArgumentException("A booking must have a positive duration.", nameof(durationMinutes));
        if (startMinute < 0 || startMinute + durationMinutes > 1440)
            throw new ArgumentException("A booking must start and end within the same day.", nameof(startMinute));
        if (string.IsNullOrWhiteSpace(customerName))
            throw new ArgumentException("Customer name cannot be empty.", nameof(customerName));
        if (origin == BookingOrigin.Counter && createdBy is null)
            throw new ArgumentException("A counter booking must record the operator that created it.", nameof(createdBy));
        if (origin == BookingOrigin.Portal && personId is null)
            throw new ArgumentException("A portal booking must be linked to a person.", nameof(personId));

        var trimmedName = customerName.Trim();
        if (trimmedName.Length > 120)
            throw new ArgumentOutOfRangeException(nameof(customerName), "Customer name cannot exceed 120 characters.");

        var trimmedPhone = customerPhone?.Trim();
        if (trimmedPhone is { Length: > 40 })
            throw new ArgumentOutOfRangeException(nameof(customerPhone), "Customer phone cannot exceed 40 characters.");

        Id = id;
        TenantId = tenantId;
        CourtId = courtId;
        Date = date;
        StartMinute = startMinute;
        DurationMinutes = durationMinutes;
        Price = price;
        CustomerName = trimmedName;
        CustomerPhone = string.IsNullOrEmpty(trimmedPhone) ? null : trimmedPhone;
        PersonId = personId;
        Status = status;
        Origin = origin;
        PaymentMode = paymentMode;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    public void ConfirmPayment()
    {
        if (Status is not (BookingStatus.PendingPayment or BookingStatus.Expired))
            throw new InvalidOperationException("Only a pending or expired hold can be confirmed by a payment.");
        Status = BookingStatus.Confirmed;
    }

    public void Expire()
    {
        if (Status != BookingStatus.PendingPayment)
            throw new InvalidOperationException("Only a pending hold can expire.");
        Status = BookingStatus.Expired;
    }

    public void Cancel(DateTimeOffset at, string reason)
    {
        if (Status == BookingStatus.Cancelled)
            throw new InvalidOperationException("The booking is already cancelled.");

        var trimmedReason = reason?.Trim();
        if (string.IsNullOrEmpty(trimmedReason))
            throw new ArgumentException("Cancelling a booking requires a reason.", nameof(reason));
        if (trimmedReason.Length > CancellationReasonMaxLength)
            throw new ArgumentOutOfRangeException(nameof(reason),
                $"Cancellation reason cannot exceed {CancellationReasonMaxLength} characters.");

        Status = BookingStatus.Cancelled;
        CancelledAt = at;
        CancellationReason = trimmedReason;
    }

    private Booking()
    {
        CustomerName = null!;
    }
}
