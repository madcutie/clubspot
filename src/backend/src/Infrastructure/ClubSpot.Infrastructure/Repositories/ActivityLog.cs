using System.Text.Json;
using System.Text.Json.Serialization;
using ClubSpot.Application.Bookings;
using ClubSpot.Application.Core.Activity;
using ClubSpot.Domain.Core.Activity;
using ClubSpot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ClubSpot.SharedKernel.Activity;
using ClubSpot.SharedKernel.Tenancy;
using ClubSpot.SharedKernel.Time;

namespace ClubSpot.Infrastructure.Repositories;

internal sealed class ActivityLog(
    ClubSpotDbContext db, ITenantContext tenantContext, IActivityActor actor, IClock clock) : IActivityLog
{
    // Same shape the API publishes: camelCase keys and values.
    private static readonly JsonSerializerOptions Payload = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    // Composed here because a module never reads another module's catalog: infrastructure wires
    // both, the same way it wires every other contract.
    private static readonly IReadOnlySet<string> RequireReason =
        new HashSet<string>([.. CoreActivity.RequireReason, .. BookingActivity.RequireReason]);

    public void Record(ActivityRecord record)
    {
        if (RequireReason.Contains(record.Type) && string.IsNullOrWhiteSpace(record.Reason))
            throw new ArgumentException($"Activity type '{record.Type}' requires a reason.", nameof(record));

        var who = actor.Current;
        db.Set<ActivityLogEntry>().Add(new ActivityLogEntry(
            Guid.NewGuid(), tenantContext.Current, clock.UtcNow, record.Type, who.Source,
            who.UserId, who.Name, record.Reason, record.BookingId, record.PersonId, record.PaymentId,
            Serialize(record.Data)));
    }

    // A key with nothing in it is noise the reader has to skip; dropped here so no caller has to
    // build its dictionary conditionally.
    private static string Serialize(IReadOnlyDictionary<string, object?>? data) =>
        JsonSerializer.Serialize(
            data?.Where(pair => pair.Value is not null).ToDictionary(pair => pair.Key, pair => pair.Value)
                ?? [],
            Payload);

    public void DiscardPending()
    {
        foreach (var entry in db.ChangeTracker.Entries<ActivityLogEntry>().Where(entry => entry.State == EntityState.Added))
            entry.State = EntityState.Detached;
    }
}
