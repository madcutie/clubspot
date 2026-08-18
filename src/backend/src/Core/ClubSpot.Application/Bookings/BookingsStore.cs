using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Primitives;

namespace ClubSpot.Application.Bookings;

public enum BookingCreateOutcome { Created, UnknownCourt, InvalidSlot, SlotTaken }

public enum BookingCancelOutcome { NotFound, Cancelled }

public sealed record BookingCreateInput(Guid CourtId, DateOnly Date, int StartMinute, int DurationMinutes,
    string CustomerName, string? CustomerPhone, string? CustomerEmail, BookingOrigin Origin,
    PaymentMode PaymentMode, Guid? CreatedBy);

public sealed record BookingCreateResult(BookingCreateOutcome Outcome, Guid Id, Money Price,
    BookingStatus Status = BookingStatus.Confirmed, Money ChargeAmount = default, DateTimeOffset? ExpiresAt = null);

public enum PaymentApplyOutcome { Confirmed, Rejected, AlreadyProcessed, Orphaned, UnknownBooking }

public sealed record PaymentNotification(Guid BookingId, string Gateway, string ExternalId, bool Approved, decimal? Amount);

public sealed record BookingSnapshot(Guid Id, Guid CourtId, string CourtName, Sport Sport, DateOnly Date,
    int StartMinute, int DurationMinutes, decimal Price, decimal PaidAmount, BookingStatus Status,
    PaymentMode PaymentMode, DateTimeOffset? ExpiresAt);

public interface IBookingsStore
{
    Task<BookingCreateResult> CreateAsync(BookingCreateInput input, CancellationToken cancellationToken);
    Task<BookingCancelOutcome> CancelAsync(Guid id, CancellationToken cancellationToken);
    Task<PaymentApplyOutcome> ApplyPaymentAsync(PaymentNotification notification, PaymentSource source, CancellationToken cancellationToken);
    Task<BookingSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken);
    // Reconciliation candidates: online bookings still unpaid on our side.
    Task<IReadOnlyList<Guid>> GetUnsettledOnlineBookingIdsAsync(DateTimeOffset since, int limit, CancellationToken cancellationToken);
}
