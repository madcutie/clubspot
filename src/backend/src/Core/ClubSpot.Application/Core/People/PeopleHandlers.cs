using ClubSpot.Domain.Core.People;
using ClubSpot.SharedKernel.Primitives;
using ClubSpot.SharedKernel.Tenancy;
using ClubSpot.SharedKernel.Time;

namespace ClubSpot.Application.Core.People;

public sealed class CreatePersonHandler(IPersonRepository repository, ITenantContext tenantContext, IClock clock)
{
    public async Task<Person> HandleAsync(string name, string phone, string email, Sport sport, Guid? createdBy, CancellationToken cancellationToken)
    {
        var person = new Person(Guid.NewGuid(), tenantContext.Current, name, phone, email, PersonOrigin.Counter,
            sport, Money.Zero(), createdBy, clock);
        repository.Add(person);
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

public sealed class AddNoteHandler(IPersonRepository repository, IClock clock)
{
    public async Task<Note?> HandleAsync(Guid personId, string text, Guid authorUserId, CancellationToken cancellationToken)
    {
        var person = await repository.FindAsync(personId, cancellationToken);
        if (person is null) return null;
        var note = person.AddNote(text, authorUserId, clock);
        await repository.SaveChangesAsync(cancellationToken);
        return note;
    }
}

public sealed class RegisterPersonPaymentHandler(IPersonRepository repository)
{
    public async Task<Money?> HandleAsync(Guid personId, CancellationToken cancellationToken)
    {
        var person = await repository.FindAsync(personId, cancellationToken);
        if (person is null) return null;
        var paid = person.RegisterPayment();
        await repository.SaveChangesAsync(cancellationToken);
        return paid;
    }
}
