using ClubSpot.SharedKernel.Primitives;

namespace ClubSpot.Application.Bookings;

public enum BookingCreateOutcome { Created, UnknownCourt, InvalidSlot, SlotTaken }

public enum BookingCancelOutcome { NotFound, Cancelled }

public sealed record BookingCreateInput(Guid CourtId, DateOnly Date, int StartMinute, int DurationMinutes,
    string CustomerName, string? CustomerPhone, Guid CreatedBy);

public sealed record BookingCreateResult(BookingCreateOutcome Outcome, Guid Id, Money Price);

public interface IBookingsStore
{
    Task<BookingCreateResult> CreateAsync(BookingCreateInput input, CancellationToken cancellationToken);
    Task<BookingCancelOutcome> CancelAsync(Guid id, CancellationToken cancellationToken);
}
