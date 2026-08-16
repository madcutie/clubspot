using ClubSpot.Domain.Core.People;

namespace ClubSpot.Application.Core.People;

public interface IPersonRepository
{
    Task<Person?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Person>> FindAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    void Add(Person person);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
