using System.Globalization;
using System.Text;
using ClubSpot.SharedKernel.Primitives;
using ClubSpot.SharedKernel.Tenancy;
using ClubSpot.SharedKernel.Time;

namespace ClubSpot.Domain.Core.People;

public sealed class Person : ITenantOwned
{
    private readonly List<Note> _notes = [];

    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; }
    public string SearchName { get; private set; }
    public string Phone { get; private set; }
    public string PhoneDigits { get; private set; }
    public string Email { get; private set; }
    public PersonOrigin Origin { get; private set; }
    public bool IsBlocked { get; private set; }
    // Provisional and known to break ADR-0012: money belongs to the finance module, not to a core
    // identity. Debt is a plain balance here and RegisterPayment wipes it with no counter-entry.
    // Moves out when the finance side is defined; nothing new hangs off it meanwhile.
    public Money Debt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public IReadOnlyCollection<Note> Notes => _notes;

    public Person(Guid id, TenantId tenantId, string name, string phone, string email, PersonOrigin origin,
        Money debt, Guid? createdBy, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be empty.", nameof(name));
        if (debt.IsNegative) throw new ArgumentOutOfRangeException(nameof(debt), "Debt cannot be negative.");

        Id = id;
        TenantId = tenantId;
        Name = name.Trim();
        SearchName = Normalize(Name);
        Phone = phone.Trim();
        PhoneDigits = Digits(Phone);
        Email = email.Trim().ToLowerInvariant();
        Origin = origin;
        Debt = debt;
        CreatedBy = createdBy;
        CreatedAt = clock.UtcNow;
    }

    public void SetBlocked(bool blocked) => IsBlocked = blocked;

    public Note AddNote(string text, Guid authorUserId, IClock clock)
    {
        var note = new Note(Guid.NewGuid(), TenantId, Id, text, authorUserId, clock.UtcNow);
        _notes.Add(note);
        return note;
    }

    public Money RegisterPayment()
    {
        var paid = Debt;
        Debt = Money.Zero(Debt.Currency);
        return paid;
    }

    private Person()
    {
        Name = null!;
        SearchName = null!;
        Phone = null!;
        PhoneDigits = null!;
        Email = null!;
    }

    private static string Digits(string value) => new(value.Where(char.IsDigit).ToArray());

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(character));
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
