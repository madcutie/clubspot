using ClubSpot.Application.Core.Activity;
using ClubSpot.Application.Core;
using ClubSpot.Domain.Core.People;
using ClubSpot.SharedKernel.Primitives;
using ClubSpot.SharedKernel.Tenancy;
using ClubSpot.SharedKernel.Time;

namespace ClubSpot.Application.Core.People;

public sealed class CreatePersonHandler(IPersonRepository repository, ITenantContext tenantContext, IClubSettings clubSettings, IClock clock, IActivityLog activityLog)
{
    public async Task<Person> HandleAsync(string name, string phone, string email, Guid? createdBy, CancellationToken cancellationToken)
    {
        var club = await clubSettings.GetAsync(cancellationToken);
        var person = new Person(Guid.NewGuid(), tenantContext.Current, name, phone, email, PersonOrigin.Counter,
            Money.Zero(club.Currency), createdBy, clock);
        repository.Add(person);
        activityLog.Record(new ActivityRecord(CoreActivity.PersonCreated, PersonId: person.Id,
            Data: new Dictionary<string, object?> { ["origin"] = PersonOrigin.Counter }));
        await repository.SaveChangesAsync(cancellationToken);
        return person;
    }
}

public sealed class BlockPeopleHandler(IPersonRepository repository)
{
    public async Task<int> HandleAsync(IReadOnlyCollection<Guid> ids, bool blocked, CancellationToken cancellationToken)
    {
        var people = await repository.FindAsync(ids, cancellationToken);
        foreach (var person in people) person.SetBlocked(blocked);
        await repository.SaveChangesAsync(cancellationToken);
        return people.Count;
    }
}

public sealed class AddNoteHandler(IPersonRepository repository, IClock clock, IActivityLog activityLog)
{
    public async Task<Note?> HandleAsync(Guid personId, string text, Guid authorUserId, CancellationToken cancellationToken)
    {
        var person = await repository.FindAsync(personId, cancellationToken);
        if (person is null) return null;
        var note = person.AddNote(text, authorUserId, clock);
        activityLog.Record(new ActivityRecord(CoreActivity.PersonNoteAdded, PersonId: person.Id));
        await repository.SaveChangesAsync(cancellationToken);
        return note;
    }
}

public sealed class RegisterPersonPaymentHandler(IPersonRepository repository, IActivityLog activityLog)
{
    public async Task<Money?> HandleAsync(Guid personId, CancellationToken cancellationToken)
    {
        var person = await repository.FindAsync(personId, cancellationToken);
        if (person is null) return null;
        var paid = person.RegisterPayment();
        activityLog.Record(new ActivityRecord(CoreActivity.PersonPaymentRegistered, PersonId: person.Id,
            Data: new Dictionary<string, object?> { ["amount"] = paid.Amount, ["currency"] = paid.Currency }));
        await repository.SaveChangesAsync(cancellationToken);
        return paid;
    }
}
