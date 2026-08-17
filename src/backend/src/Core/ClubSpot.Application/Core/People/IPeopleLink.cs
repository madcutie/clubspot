namespace ClubSpot.Application.Core.People;

// Contract for other modules: resolve a contact to a person, creating one when nobody matches.
// Email wins over phone; an existing person is never mutated. The add is not flushed — it
// commits with the caller's SaveChanges so person and link land in one transaction.
public interface IPeopleLink
{
    Task<Guid> EnsurePersonAsync(string name, string phone, string? email, CancellationToken cancellationToken);
}
