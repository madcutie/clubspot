using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Primitives;

namespace ClubSpot.Application.Bookings;

public enum BookingCreateOutcome { Created, UnknownCourt, InvalidSlot, SlotTaken }

public enum BookingCancelOutcome { NotFound, Cancelled, MissingReason }

public sealed record BookingCreateInput(Guid CourtId, DateOnly Date, int StartMinute, int DurationMinutes,
    string CustomerName, string? CustomerPhone, string? CustomerEmail, BookingOrigin Origin,
    PaymentMode PaymentMode, Guid? CreatedBy);

public sealed record BookingCreateResult(BookingCreateOutcome Outcome, Guid Id, Money Price,
    BookingStatus Status = BookingStatus.Confirmed, Money ChargeAmount = default, DateTimeOffset? ExpiresAt = null);

public enum PaymentApplyOutcome { Confirmed, Rejected, Pending, AlreadyProcessed, Orphaned, UnknownBooking }

public enum HoldReleaseOutcome { Released, NotPending, NotFound }

// Outcome, not a bool: a provider that has taken the payment but not decided it yet must not be
// recorded as a rejection — that is what burned the idempotency key and lost the approval after it.
public sealed record PaymentNotification(Guid BookingId, string Provider, PaymentRail Rail, string ExternalId,
    PaymentOutcome Outcome, decimal? Amount, string? Currency = null);

public sealed record CheckoutIssued(Guid BookingId, string Provider, string Url, Money Amount,
    DateTimeOffset ExpiresAt);

// One attempt the provider actually reported, as the payer would read it on a receipt: no internal
// ids, no orphan reason — what that means to the club is the club's business, not the customer's.
public sealed record BookingPaymentLine(DateTimeOffset At, string Provider, string ExternalId,
    decimal Amount, string Currency, PaymentKind Kind, PaymentStatus Status);

public sealed record BookingSnapshot(Guid Id, Guid CourtId, string CourtName, Sport Sport, DateOnly Date,
    int StartMinute, int DurationMinutes, decimal Price, decimal PaidAmount, BookingStatus Status,
    PaymentMode PaymentMode, DateTimeOffset? ExpiresAt, DateTimeOffset CreatedAt,
    IReadOnlyList<BookingPaymentLine> Payments);

public interface IBookingsStore
{
    Task<BookingCreateResult> CreateAsync(BookingCreateInput input, CancellationToken cancellationToken);
    Task<BookingCancelOutcome> CancelAsync(Guid id, string reason, CancellationToken cancellationToken);
    // Abandoned checkout: frees a pending hold right away instead of waiting out its TTL.
    Task<HoldReleaseOutcome> ReleaseHoldAsync(Guid id, CancellationToken cancellationToken);
    Task<PaymentApplyOutcome> ApplyPaymentAsync(PaymentNotification notification, PaymentSource source, CancellationToken cancellationToken);
    Task<BookingSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken);

    // Every link handed out is kept: it answers "ya le mandé el link" without asking the customer,
    // and lets the same link be shown again instead of asking the provider for another one.
    Task RecordCheckoutIssuedAsync(CheckoutIssued issued, CancellationToken cancellationToken);
    // A link already handed out for this same charge and not yet expired. Asking the provider for
    // another one instead leaves two payable links alive for the same money.
    Task<CheckoutIssued?> FindLiveCheckoutAsync(Guid bookingId, string provider, Money amount,
        DateTimeOffset asOf, CancellationToken cancellationToken);
    // Reconciliation candidates: online bookings still unpaid on our side.
    Task<IReadOnlyList<Guid>> GetUnsettledOnlineBookingIdsAsync(DateTimeOffset since, int limit, CancellationToken cancellationToken);
}
