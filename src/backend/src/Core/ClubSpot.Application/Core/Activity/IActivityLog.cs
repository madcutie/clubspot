namespace ClubSpot.Application.Core.Activity;

// The write side of the activity log. The actor does not travel in the record: the implementation
// takes it from IActivityActor, so no caller can lie about who acted (ADR-0017).
public interface IActivityLog
{
    // Does not call SaveChanges: the entry commits with the fact or does not commit at all.
    void Record(ActivityRecord record);

    // Only legitimate use: a SaveChanges that failed and is being retried with a different outcome,
    // where the entries already recorded describe a fact that did not happen.
    void DiscardPending();
}

public sealed record ActivityRecord(
    string Type,
    Guid? BookingId = null,
    Guid? PersonId = null,
    Guid? PaymentId = null,
    string? Reason = null,
    IReadOnlyDictionary<string, object?>? Data = null);

// A type already emitted never changes meaning: if the fact changes, a new type is added
// (ADR-0017 §4). Each module declares its own catalog; the table stores a string.
public static class CoreActivity
{
    public const string PersonCreated = "personCreated";
    public const string PersonBlocked = "personBlocked";
    public const string PersonUnblocked = "personUnblocked";
    public const string PersonNoteAdded = "personNoteAdded";
    public const string PersonPaymentRegistered = "personPaymentRegistered";

    // Destructive types demand a reason. Checked in the port, not left to each caller to remember.
    public static readonly IReadOnlySet<string> RequireReason = new HashSet<string> { PersonBlocked };
}
