using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Primitives;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.UnitTests.Bookings;

public sealed class CourtTests
{
    [Fact]
    public void A_court_requires_at_least_one_duration()
    {
        Assert.Throws<ArgumentException>(() => new Court(
            Guid.NewGuid(), TenantId.From(Guid.NewGuid()), Sport.Padel, 1, "Court 1", "", false, true,
            Guid.NewGuid(), [], 30, 0, Money.Of(100m, "ARS"), Money.Of(100m, "ARS"), 1080));
    }

    [Fact]
    public void A_court_cannot_mix_currencies_between_day_and_night_price()
    {
        Assert.Throws<ArgumentException>(() => new Court(
            Guid.NewGuid(), TenantId.From(Guid.NewGuid()), Sport.Padel, 1, "Court 1", "", false, true,
            Guid.NewGuid(), [60], 30, 0, Money.Of(100m, "ARS"), Money.Of(100m, "USD"), 1080));
    }
}
