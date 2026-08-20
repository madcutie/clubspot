namespace ClubSpot.SharedKernel.Activity;

// Where a fact entered the system. Answers the first question asked when something went wrong:
// did somebody do this, or did it happen on its own (ADR-0017).
public enum ActivitySource
{
    Counter,
    Portal,
    Webhook,
    Job,
    System
}

// Name is a snapshot, not a join: the log has to read the same in five years, after the user was
// renamed or deactivated. UserId is null exactly when the actor is the system.
public sealed record ActivityActor(Guid? UserId, string Name, ActivitySource Source)
{
    public const string SystemName = "system";

    public static ActivityActor Webhook(string provider) => new(null, provider, ActivitySource.Webhook);

    public static ActivityActor Job(string job) => new(null, job, ActivitySource.Job);

    public static ActivityActor Portal() => new(null, SystemName, ActivitySource.Portal);
}
