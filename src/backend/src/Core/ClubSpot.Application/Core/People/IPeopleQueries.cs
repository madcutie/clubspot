using ClubSpot.Domain.Core.People;
using ClubSpot.SharedKernel.Primitives;

namespace ClubSpot.Application.Core.People;

public interface IPeopleQueries
{
    Task<PeoplePage> SearchAsync(PeopleSearch search, CancellationToken cancellationToken);
    Task<PersonDetails?> GetAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record PeopleSearch(string Query, PeopleFilter Filter, int Page);
public enum PeopleFilter { All, WithoutBookings, Counter, Debt }
public sealed record PersonListItem(Guid Id, string Name, string Phone, string Email, PersonOrigin Origin,
    int Bookings, DateTimeOffset? LastBookingAt, Money Debt, bool IsBlocked, DateTimeOffset CreatedAt);
public sealed record PeoplePage(IReadOnlyList<PersonListItem> Items, int Total, int Page, int Pages, int Census,
    int NeedsAttention, Money TotalDebt, IReadOnlyDictionary<PeopleFilter, int> Totals);
public sealed record PersonNote(string Text, string AuthorName, DateTimeOffset CreatedAt);
public sealed record PersonDetails(PersonListItem Person, IReadOnlyList<PersonNote> Notes);
