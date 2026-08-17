using ClubSpot.Application.Core.People;
using ClubSpot.Domain.Core.People;
using ClubSpot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClubSpot.Infrastructure.Repositories;

internal sealed class PersonRepository(ClubSpotDbContext db) : IPersonRepository
{
    public Task<Person?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.People.Include(person => person.Notes).SingleOrDefaultAsync(person => person.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Person>> FindAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await db.People.Where(person => ids.Contains(person.Id)).ToListAsync(cancellationToken);

    public void Add(Person person) => db.People.Add(person);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
