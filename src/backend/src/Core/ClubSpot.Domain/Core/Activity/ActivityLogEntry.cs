using ClubSpot.SharedKernel.Activity;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Domain.Core.Activity;

// One entry of the activity log: append-only, built once and never touched again (ADR-0017).
// The references are plain columns and not relations on purpose — an entry has to survive the
// deletion of its subject, or purging a booking would erase its own history.
public sealed class ActivityLogEntry : ITenantOwned
{
    public const int TypeMaxLength = 60;
    public const int ActorNameMaxLength = 120;
    public const int ReasonMaxLength = 300;

    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string Type { get; private set; }
    public ActivitySource Source { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string ActorName { get; private set; }
    public string? Reason { get; private set; }
    public Guid? BookingId { get; private set; }
    public Guid? PersonId { get; private set; }
    public Guid? PaymentId { get; private set; }
    public string Data { get; private set; }

    public ActivityLogEntry(Guid id, TenantId tenantId, DateTimeOffset occurredAt, string type,
        ActivitySource source, Guid? actorUserId, string actorName, string? reason,
        Guid? bookingId, Guid? personId, Guid? paymentId, string data)
    {
        if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException("Activity type cannot be empty.", nameof(type));
        if (type.Length > TypeMaxLength) throw new ArgumentOutOfRangeException(nameof(type), $"Activity type cannot exceed {TypeMaxLength} characters.");
        if (string.IsNullOrWhiteSpace(actorName)) throw new ArgumentException("Actor name cannot be empty.", nameof(actorName));
        if (reason is { Length: > ReasonMaxLength }) throw new ArgumentOutOfRangeException(nameof(reason), $"Reason cannot exceed {ReasonMaxLength} characters.");

        Id = id;
        TenantId = tenantId;
        OccurredAt = occurredAt;
        Type = type;
        Source = source;
        ActorUserId = actorUserId;
        ActorName = actorName.Length > ActorNameMaxLength ? actorName[..ActorNameMaxLength] : actorName;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        BookingId = bookingId;
        PersonId = personId;
        PaymentId = paymentId;
        Data = data;
    }

    private ActivityLogEntry()
    {
        Type = null!;
        ActorName = null!;
        Data = null!;
    }
}
