using ClubSpot.SharedKernel.Primitives;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Domain.Bookings;

// A payment link handed to the customer. Append-only: reissuing writes another row, so "ya le
// mandé el link" is answered from here and not from the activity log.
public sealed class BookingCheckout : ITenantOwned
{
    public const int UrlMaxLength = 500;

    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Guid BookingId { get; private set; }
    public string Provider { get; private set; }
    public string Url { get; private set; }
    public Money Amount { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }

    public BookingCheckout(Guid id, TenantId tenantId, Guid bookingId, string provider, string url,
        Money amount, DateTimeOffset expiresAt, DateTimeOffset issuedAt)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider cannot be empty.", nameof(provider));
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("A checkout without a link is not a checkout.", nameof(url));
        if (url.Length > UrlMaxLength)
            throw new ArgumentOutOfRangeException(nameof(url), $"Url cannot exceed {UrlMaxLength} characters.");
        if (expiresAt <= issuedAt)
            throw new ArgumentException("A checkout must expire after it was issued.", nameof(expiresAt));

        Id = id;
        TenantId = tenantId;
        BookingId = bookingId;
        Provider = provider;
        Url = url;
        Amount = amount;
        ExpiresAt = expiresAt;
        IssuedAt = issuedAt;
    }

    private BookingCheckout()
    {
        Provider = null!;
        Url = null!;
    }
}
