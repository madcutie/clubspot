using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Domain.Core;

// Not ITenantOwned on purpose: this is the tenant registry, the single whitelisted table.
public sealed class Club
{
    public TenantId Id { get; private set; }
    public string Slug { get; private set; }
    public string Name { get; private set; }
    public string? Venue { get; private set; }
    public string TimeZone { get; private set; }
    public string Currency { get; private set; }
    public int DepositPercent { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public Club(
        TenantId id,
        string slug,
        string name,
        string? venue,
        string timeZone,
        string currency,
        int depositPercent,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(slug) ||
            !slug.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-'))
            throw new ArgumentException(
                $"Invalid club slug: '{slug}'. Only lowercase ASCII letters, digits and hyphens.", nameof(slug));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Club name cannot be empty.", nameof(name));

        if (string.IsNullOrWhiteSpace(timeZone))
            throw new ArgumentException("Time zone cannot be empty.", nameof(timeZone));

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(currency));

        if (depositPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(depositPercent), "Deposit percent must be between 0 and 100.");

        Id = id;
        Slug = slug;
        Name = name.Trim();
        Venue = string.IsNullOrWhiteSpace(venue) ? null : venue.Trim();
        TimeZone = timeZone;
        Currency = currency.ToUpperInvariant();
        DepositPercent = depositPercent;
        CreatedAt = createdAt;
    }

    private Club()
    {
        Slug = null!;
        Name = null!;
        TimeZone = null!;
        Currency = null!;
    }
}
