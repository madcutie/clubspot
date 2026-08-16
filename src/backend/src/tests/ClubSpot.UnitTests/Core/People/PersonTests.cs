using ClubSpot.Domain.Core.People;
using ClubSpot.SharedKernel.Primitives;
using ClubSpot.SharedKernel.Tenancy;
using ClubSpot.SharedKernel.Time;

namespace ClubSpot.UnitTests.Core.People;

public sealed class PersonTests
{
    [Fact]
    public void Registering_a_payment_clears_the_debt()
    {
        var person = CreatePerson(Money.Of(12_500));

        var paid = person.RegisterPayment();

        Assert.Equal(12_500, paid.Amount);
        Assert.True(person.Debt.IsZero);
    }

    [Fact]
    public void Adding_a_note_preserves_its_author_and_timestamp()
    {
        var clock = new TestClock(DateTimeOffset.Parse("2026-08-15T12:00:00Z"));
        var person = CreatePerson(Money.Zero(), clock);
        var author = Guid.NewGuid();

        var note = person.AddNote("Call before the next booking.", author, clock);

        Assert.Equal(author, note.AuthorUserId);
        Assert.Equal(clock.UtcNow, note.CreatedAt);
        Assert.Contains(note, person.Notes);
    }

    [Fact]
    public void Person_normalizes_searchable_name_and_phone_digits()
    {
        var person = new Person(Guid.NewGuid(), TenantId.From(Guid.NewGuid()), "  Julián Gómez  ", "362 415-8890", "", PersonOrigin.Counter,
            Sport.Padel, Money.Zero(), null, new TestClock(DateTimeOffset.UtcNow));

        Assert.Equal("julian gomez", person.SearchName);
        Assert.Equal("3624158890", person.PhoneDigits);
    }

    private static Person CreatePerson(Money debt, IClock? clock = null) => new(Guid.NewGuid(), TenantId.From(Guid.NewGuid()), "Person",
        "362 400-0000", "person@clubspot.test", PersonOrigin.Counter, Sport.Padel, debt, null,
        clock ?? new TestClock(DateTimeOffset.UtcNow));

    private sealed class TestClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
