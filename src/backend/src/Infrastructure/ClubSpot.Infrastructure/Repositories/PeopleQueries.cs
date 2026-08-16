using ClubSpot.Application.Core.People;
using ClubSpot.Domain.Core;
using ClubSpot.Domain.Core.People;
using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace ClubSpot.Infrastructure.Repositories;

internal sealed class PeopleQueries(CoreDbContext db) : IPeopleQueries
{
    private const int PageSize = 14;

    public async Task<PeoplePage> SearchAsync(PeopleSearch search, CancellationToken cancellationToken)
    {
        var people = db.People.AsNoTracking();
        var query = search.Query.Trim();
        var digits = new string(query.Where(char.IsDigit).ToArray());
        if (digits.Length > 0) people = people.Where(person => person.PhoneDigits.Contains(digits));
        else if (query.Length > 0)
        {
            var normalized = Normalize(query);
            var email = query.ToLowerInvariant();
            people = people.Where(person => person.SearchName.Contains(normalized) || person.Email.Contains(email));
        }

        var census = await db.People.CountAsync(cancellationToken);
        var attention = await db.People.CountAsync(person => person.IsBlocked || person.Debt.Amount > 0, cancellationToken);
        var totalDebt = await db.People.Select(person => (decimal?)person.Debt.Amount).SumAsync(cancellationToken) ?? 0m;
        var all = await db.People.CountAsync(cancellationToken);
        var counter = await db.People.CountAsync(person => person.Origin == PersonOrigin.Counter, cancellationToken);
        var debt = attention;

        people = search.Filter switch
        {
            PeopleFilter.Counter => people.Where(person => person.Origin == PersonOrigin.Counter),
            PeopleFilter.Debt => people.Where(person => person.IsBlocked || person.Debt.Amount > 0),
            _ => people
        };
        var total = await people.CountAsync(cancellationToken);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        var page = Math.Clamp(search.Page, 0, pages - 1);
        var items = await people.OrderBy(person => person.Name).Skip(page * PageSize).Take(PageSize)
            .Select(person => new PersonListItem(person.Id, person.Name, person.Phone, person.Email, person.Origin,
                person.PreferredSport, 0, null, person.Debt, person.IsBlocked, person.CreatedAt)).ToListAsync(cancellationToken);
        var withoutBookings = all;
        return new PeoplePage(items, total, page, pages, census, attention, Money.Of(totalDebt),
            new Dictionary<PeopleFilter, int> { [PeopleFilter.All] = all, [PeopleFilter.WithoutBookings] = withoutBookings, [PeopleFilter.Counter] = counter, [PeopleFilter.Debt] = debt });
    }

    public async Task<PersonDetails?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var person = await db.People.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (person is null) return null;
        var notes = await (from note in db.Notes.AsNoTracking()
                           join user in db.Users.AsNoTracking() on note.AuthorUserId equals user.Id
                           where note.PersonId == id
                           orderby note.CreatedAt descending
                           select new PersonNote(note.Text, user.Name, note.CreatedAt)).ToListAsync(cancellationToken);
        return new PersonDetails(ToItem(person), notes);
    }

    private static PersonListItem ToItem(Person person) => new(person.Id, person.Name, person.Phone, person.Email,
        person.Origin, person.PreferredSport, 0, null, person.Debt, person.IsBlocked, person.CreatedAt);

    private static string Normalize(string value) => string.Concat(value.Normalize(System.Text.NormalizationForm.FormD)
        .Where(character => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.NonSpacingMark)).ToLowerInvariant();
}
