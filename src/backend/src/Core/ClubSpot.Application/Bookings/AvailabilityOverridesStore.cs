using ClubSpot.Domain.Bookings;

namespace ClubSpot.Application.Bookings;

public enum OverrideCreateOutcome { Created, UnknownCourt, NoDates, DuplicateDates, InvalidWindows, ReasonTooLong }

public sealed record OverrideWindowInput(int OpensAtMinute, int ClosesAtMinute);

public sealed record OverrideCreateInput(Guid? CourtId, IReadOnlyList<DateOnly> Dates,
    IReadOnlyList<OverrideWindowInput> Windows, string? Reason, Guid CreatedBy);

public sealed record OverrideCreateResult(OverrideCreateOutcome Outcome, Guid Id);

public interface IAvailabilityOverridesStore
{
    Task<IReadOnlyList<AvailabilityOverride>> ListAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<OverrideCreateResult> CreateAsync(OverrideCreateInput input, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
